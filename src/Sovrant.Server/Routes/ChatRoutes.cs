using System.Text;
using System.Text.Json;
using Sovrant.Api.Routing;
using Sovrant.Runtime.Conversation;
using Sovrant.Server.OpenAi;
using Sovrant.Server.ServerConfig;
using Sovrant.Server.Streaming;
using Sovrant.Tools;

namespace Sovrant.Server.Routes;

/// <summary>Registers the <c>POST /v1/chat/completions</c> endpoint.</summary>
internal static class ChatRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/chat/completions", HandleAsync);
    }

    private static async Task HandleAsync(
        HttpContext ctx,
        ChatCompletionRequest req,
        IRuntimeSessionPool sessionPool,
        IConversationRuntime transientRuntime,
        MutableServerConfig serverConfig,
        ISmartRouter router,
        ToolRegistrar toolRegistrar,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        // Seed tools (no-op if already registered).
        toolRegistrar.RegisterAll();

        // Initialize router (no-op if already done).
        await router.InitializeAsync(ct).ConfigureAwait(false);

        // Apply provider pin from config.
        var pinned = serverConfig.PinnedProvider;
        if (pinned is not null)
            await router.PinProviderAsync(pinned, ct).ConfigureAwait(false);

        // With a session ID → use the pool (in-memory history persists across requests).
        // Without a session ID → use the transient runtime injected by DI (stateless one-shot).
        IConversationRuntime runtime;
        if (!string.IsNullOrWhiteSpace(req.SessionId))
        {
            runtime = await sessionPool.GetOrCreateAsync(req.SessionId, ct).ConfigureAwait(false);
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
        var model = req.Model ?? serverConfig.Model;

        // Apply per-request model override to the live config so ConversationRuntime picks it up.
        if (req.Model is not null)
            serverConfig.Model = req.Model;

        if (req.Stream)
        {
            await StreamResponseAsync(ctx, runtime, completionId, model, userMessage, ct).ConfigureAwait(false);
        }
        else
        {
            await BufferedResponseAsync(ctx, runtime, completionId, model, userMessage, ct).ConfigureAwait(false);
        }
    }

    private static async Task StreamResponseAsync(
        HttpContext ctx,
        IConversationRuntime runtime,
        string completionId,
        string model,
        string userMessage,
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
