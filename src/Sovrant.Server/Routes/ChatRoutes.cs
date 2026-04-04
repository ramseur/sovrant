using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Auth;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Tools;
using Sovrant.Server.OpenAi;
using Sovrant.Server.Permissions;
using Sovrant.Server.ServerConfig;
using Sovrant.Server.Streaming;
using Sovrant.Tools;

namespace Sovrant.Server.Routes;

/// <summary>Registers the <c>POST /v1/chat/completions</c> endpoint.</summary>
internal static class ChatRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/chat/completions", HandleAsync)
            .RequireRateLimiting("per-session");
    }

    private static async Task HandleAsync(
        HttpContext ctx,
        ChatCompletionRequest req,
        IRuntimeSessionPool sessionPool,
        IConversationRuntime transientRuntime,
        MutableServerConfig serverConfig,
        ISmartRouter router,
        ToolRegistrar toolRegistrar,
        IHttpClientFactory httpClientFactory,
        IToolExecutor toolExecutor,
        IToolRegistry toolRegistry,
        ISessionStore sessionStore,
        SovrantConfig sovrantConfig,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        // Seed tools (no-op if already registered).
        toolRegistrar.RegisterAll();

        // Determine whether per-request credentials override the global provider.
        var hasScopedCredentials = !string.IsNullOrWhiteSpace(req.XApiKey);
        var scopedBaseUrl = req.XBaseUrl;

        ISmartRouter activeRouter;
        if (hasScopedCredentials)
        {
            // Build a request-scoped provider + router using the client-supplied credentials.
            // The server never logs or persists x_api_key.
            var baseUrl = !string.IsNullOrWhiteSpace(scopedBaseUrl)
                ? scopedBaseUrl
                : serverConfig.LlmBaseUrl;
            if (!baseUrl.EndsWith('/')) baseUrl += "/";

            var scopedHttp = httpClientFactory.CreateClient("ScopedProvider");
            scopedHttp.BaseAddress = new Uri(baseUrl);

            var scopedAuth = new ApiKeyAuthProvider(req.XApiKey!);
            var scopedLogger = loggerFactory.CreateLogger("Sovrant.Api.ScopedProvider");
            var scopedProvider = new OpenAiCompatProvider(scopedHttp, scopedAuth, scopedLogger);
            activeRouter = new ScopedSingleProviderRouter(scopedProvider);
        }
        else
        {
            // Use the global router with normal smart routing.
            activeRouter = router;
            await activeRouter.InitializeAsync(ct).ConfigureAwait(false);

            var pinned = serverConfig.PinnedProvider;
            if (pinned is not null)
                await activeRouter.PinProviderAsync(pinned, ct).ConfigureAwait(false);
        }

        // Resolve the runtime and optional per-session lock for this request.
        IConversationRuntime runtime;
        SemaphoreSlim? sessionLock = null;
        SessionConfig? sessionConfig = null;

        if (!string.IsNullOrWhiteSpace(req.SessionId))
        {
            // Session pool key includes the provider tag when per-request credentials are used
            // so two users with the same session_id but different providers stay isolated.
            var status = activeRouter.GetStatus();
            var providerTag = status.Count > 0 ? status[0].Name : "scoped";
            var poolKey = hasScopedCredentials
                ? $"{req.SessionId}::{providerTag}"
                : req.SessionId;

            var pooled = hasScopedCredentials
                ? await sessionPool.GetOrCreateAsync(poolKey, activeRouter, ct).ConfigureAwait(false)
                : await sessionPool.GetOrCreateAsync(poolKey, ct: ct).ConfigureAwait(false);

            runtime = pooled.Runtime;
            sessionLock = pooled.Lock;
            sessionConfig = pooled.Config;
        }
        else if (hasScopedCredentials)
        {
            // Stateless one-shot with scoped credentials — build a transient runtime manually.
            var runtimeLogger = loggerFactory.CreateLogger<ConversationRuntime>();
            runtime = new ConversationRuntime(
                activeRouter, toolExecutor, toolRegistry, sessionStore, sovrantConfig, runtimeLogger);
        }
        else
        {
            runtime = transientRuntime;
        }

        // The user message is the last message with role "user".
        var userMessage = req.Messages
            .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            ?.Content ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { error = "No user message found in messages array." }, ct)
                .ConfigureAwait(false);
            return;
        }

        var completionId = $"chatcmpl-{Guid.NewGuid():N}";
        var model = req.Model ?? sessionConfig?.Model ?? serverConfig.Model;

        // Apply per-request model override to the live config so ConversationRuntime picks it up.
        // Only mutate the global config when NOT using scoped credentials and no session config.
        if (req.Model is not null && !hasScopedCredentials)
            serverConfig.Model = req.Model;

        // Acquire the per-session lock to serialize concurrent turns (prevents history corruption).
        // Transient/one-shot runtimes have no lock (no shared state).
        if (sessionLock is not null)
            await sessionLock.WaitAsync(ct).ConfigureAwait(false);

        // Set the session config on the AsyncLocal so EnterPlanMode/ExitPlanMode
        // write to this session's config instead of the global singleton.
        SessionAwarePermissionModeAdapter.SetCurrent(sessionConfig);

        try
        {
            if (req.Stream)
            {
                await StreamResponseAsync(ctx, runtime, completionId, model, userMessage, sessionConfig, ct).ConfigureAwait(false);
            }
            else
            {
                await BufferedResponseAsync(ctx, runtime, completionId, model, userMessage, sessionConfig, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            SessionAwarePermissionModeAdapter.SetCurrent(null);
            sessionLock?.Release();
        }
    }

    private static async Task StreamResponseAsync(
        HttpContext ctx,
        IConversationRuntime runtime,
        string completionId,
        string model,
        string userMessage,
        SessionConfig? sessionConfig,
        CancellationToken ct)
    {
        SseWriter.SetSseHeaders(ctx.Response);

        // First chunk: role announcement.
        await SseWriter.WriteChunkAsync(ctx.Response, new ChatCompletionChunk
        {
            Id = completionId,
            Model = model,
            Choices = [new ChunkChoice { Delta = new DeltaContent { Role = "assistant" } }],
        }, ct).ConfigureAwait(false);

        await foreach (var ev in runtime.RunTurnAsync(userMessage, ct).ConfigureAwait(false))
        {
            switch (ev)
            {
                case RuntimeEvent.TextChunk { Text: var text }:
                    await SseWriter.WriteChunkAsync(ctx.Response,
                        SseWriter.TextChunk(completionId, model, text), ct).ConfigureAwait(false);
                    break;

                case RuntimeEvent.ToolUseRequested { ToolUseId: var id, ToolName: var name }:
                    await SseWriter.WriteChunkAsync(ctx.Response,
                        SseWriter.ToolUseChunk(completionId, model, name, id), ct).ConfigureAwait(false);
                    break;

                case RuntimeEvent.ToolResult { ToolUseId: var id, ToolName: var name, IsError: var err }:
                    await SseWriter.WriteChunkAsync(ctx.Response,
                        SseWriter.ToolResultChunk(completionId, model, name, id, err), ct).ConfigureAwait(false);
                    break;

                case RuntimeEvent.TurnComplete { InputTokens: var inp, OutputTokens: var outp }:
                    sessionConfig?.AddTokens(inp, outp);
                    await SseWriter.WriteChunkAsync(ctx.Response,
                        SseWriter.StopChunk(completionId, model, inp, outp), ct).ConfigureAwait(false);
                    break;

                case RuntimeEvent.RuntimeError { Message: var msg }:
                    await SseWriter.WriteChunkAsync(ctx.Response, new ChatCompletionChunk
                    {
                        Id = completionId,
                        Model = model,
                        Choices = [new ChunkChoice
                        {
                            Delta = new DeltaContent { Content = $"\n\n[Error: {msg}]" },
                            FinishReason = "stop",
                        }],
                    }, ct).ConfigureAwait(false);
                    break;
            }
        }

        await SseWriter.WriteDoneAsync(ctx.Response, ct).ConfigureAwait(false);
    }

    private static async Task BufferedResponseAsync(
        HttpContext ctx,
        IConversationRuntime runtime,
        string completionId,
        string model,
        string userMessage,
        SessionConfig? sessionConfig,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        var inputTokens = 0;
        var outputTokens = 0;

        await foreach (var ev in runtime.RunTurnAsync(userMessage, ct).ConfigureAwait(false))
        {
            switch (ev)
            {
                case RuntimeEvent.TextChunk { Text: var text }:
                    sb.Append(text);
                    break;
                case RuntimeEvent.TurnComplete { InputTokens: var inp, OutputTokens: var outp }:
                    inputTokens = inp;
                    outputTokens = outp;
                    sessionConfig?.AddTokens(inp, outp);
                    break;
            }
        }

        var response = new ChatCompletionResponse
        {
            Id = completionId,
            Model = model,
            Choices = [new ResponseChoice
            {
                Message = new ResponseMessage { Content = sb.ToString() },
            }],
            Usage = new UsageInfo
            {
                PromptTokens = inputTokens,
                CompletionTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
            },
        };

        await ctx.Response.WriteAsJsonAsync(response, ct).ConfigureAwait(false);
    }
}
