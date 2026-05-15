using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Routing;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Hooks;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Tools;
using Sovrant.Runtime.Workspaces;
using Sovrant.Server.Auth;
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
        IToolExecutor toolExecutor,
        IToolRegistry toolRegistry,
        ISessionStore sessionStore,
        SovrantConfig sovrantConfig,
        IHookRunner hookRunner,
        ILoggerFactory loggerFactory,
        IWorkspaceSettingsStore? workspaceSettings,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        // Pull the authenticated caller so sessions are owner-tagged in SQLite.
        var ownerUserId = ctx.GetUserId();

        // Seed tools (no-op if already registered).
        toolRegistrar.RegisterAll();

        // Use the global router; credentials come from the server-side keystore.
        var activeRouter = router;
        await activeRouter.InitializeAsync(ct).ConfigureAwait(false);

        var pinned = serverConfig.PinnedProvider;
        if (pinned is not null)
            await activeRouter.PinProviderAsync(pinned, ct).ConfigureAwait(false);

        // Resolve the runtime and optional per-session lock for this request.
        IConversationRuntime runtime;
        SemaphoreSlim? sessionLock = null;
        SessionConfig? sessionConfig = null;

        if (!string.IsNullOrWhiteSpace(req.SessionId))
        {
            if (!InputValidation.IsValidSessionId(req.SessionId))
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsJsonAsync(
                    new { error = "Invalid session_id: must be 1-128 alphanumeric characters, hyphens, underscores, dots, or colons." }, ct)
                    .ConfigureAwait(false);
                return;
            }

            // Phase 38 — reject attempts by a non-owner to attach to an
            // existing session. Unknown sessions (owner == null) fall through
            // so the first append creates the row with ownerUserId stamped.
            // Admin callers (static token or users.role = 'admin') bypass.
            if (ownerUserId is not null && !ctx.IsAdmin())
            {
                var recordedOwner = await sessionStore.GetOwnerAsync(req.SessionId, ct).ConfigureAwait(false);
                if (recordedOwner is not null && !string.Equals(recordedOwner, ownerUserId, StringComparison.Ordinal))
                {
                    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                    await ctx.Response.WriteAsJsonAsync(
                        new { error = $"Session '{req.SessionId}' not found." }, ct)
                        .ConfigureAwait(false);
                    return;
                }
            }

            var pooled = await sessionPool.GetOrCreateAsync(req.SessionId, scopedRouterOverride: null, ownerUserId: ownerUserId, ct: ct).ConfigureAwait(false);

            runtime = pooled.Runtime;
            sessionLock = pooled.Lock;
            sessionConfig = pooled.Config;
        }
        else
        {
            runtime = transientRuntime;
            // The transient runtime is DI-scoped (one per request) so stamping
            // it with the caller is safe — it will not leak to the next request.
            await runtime.InitializeSessionAsync(sessionId: null, ownerUserId: ownerUserId, ct).ConfigureAwait(false);
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

        if (req.Model is not null && !InputValidation.IsValidModelName(req.Model))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(
                new { error = "Invalid model name: must be 1-128 alphanumeric characters, hyphens, underscores, dots, slashes, or colons." }, ct)
                .ConfigureAwait(false);
            return;
        }

        var completionId = $"chatcmpl-{Guid.NewGuid():N}";
        var model = req.Model ?? sessionConfig?.Model ?? serverConfig.Model;

        // Apply per-request model override to the session config if present,
        // so subsequent requests in the same session default to this model.
        // Never mutate global serverConfig.Model from a per-request context — it races
        // with concurrent requests.
        if (req.Model is not null && sessionConfig is not null)
            sessionConfig.Model = req.Model;

        // Apply the per-request MCP connection allow-list to the session, both
        // in-memory (so the runtime gates this turn) and in storage (so the gate
        // survives session reload). null = leave existing gate alone.
        if (req.McpConnections is not null)
        {
            if (sessionConfig is not null)
                sessionConfig.AllowedMcpServers = req.McpConnections;
            if (req.SessionId is not null)
            {
                await sessionStore
                    .SetMcpConnectionsAsync(req.SessionId, req.McpConnections, ownerUserId, ct)
                    .ConfigureAwait(false);
            }
        }
        else if (sessionConfig is not null && sessionConfig.AllowedMcpServers is null && req.SessionId is not null)
        {
            // Hydrate the in-memory gate from storage on first turn after a reload.
            sessionConfig.AllowedMcpServers = await sessionStore
                .GetMcpConnectionsAsync(req.SessionId, ct)
                .ConfigureAwait(false);
        }

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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client disconnected — nothing to write.
        }
#pragma warning disable CA1031 // Top-level HTTP handler must catch all to return a clean error response
        catch (Exception ex)
#pragma warning restore CA1031
        {
            var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Sovrant.Server.ChatRoutes");
            logger.LogError(ex, "Unhandled error in chat completion");

            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await ctx.Response.WriteAsJsonAsync(
                    new { error = "An internal error occurred processing the request." }, CancellationToken.None)
                    .ConfigureAwait(false);
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

                // ── Phase 59 events ─────────────────────────────────────
                case RuntimeEvent.ClarificationNeeded { Question: var question }:
                    await SseWriter.WriteChunkAsync(ctx.Response,
                        SseWriter.ClarificationChunk(completionId, model, question), ct).ConfigureAwait(false);
                    break;

                case RuntimeEvent.PlanPresented { PlanId: var planId, FormattedPlan: var plan, RequiresApproval: var needsApproval }:
                    await SseWriter.WriteChunkAsync(ctx.Response,
                        SseWriter.PlanPresentedChunk(completionId, model, planId, plan, needsApproval), ct).ConfigureAwait(false);
                    break;

                case RuntimeEvent.StepProgress { Current: var current, Total: var total, Intent: var intent, Status: var status }:
                    await SseWriter.WriteChunkAsync(ctx.Response,
                        SseWriter.StepProgressChunk(completionId, model, current, total, intent, status), ct).ConfigureAwait(false);
                    break;

                // ── Phase 55 events ─────────────────────────────────────
                case RuntimeEvent.TurnCost { EstimatedUsd: var usd, Source: var source }:
                    await SseWriter.WriteChunkAsync(ctx.Response,
                        SseWriter.CostChunk(completionId, model, usd, source), ct).ConfigureAwait(false);
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
        string? clarification = null;
        SovrantEvent? presentedPlan = null;
        decimal? estimatedUsd = null;
        string? costSource = null;

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
                case RuntimeEvent.ClarificationNeeded { Question: var question }:
                    clarification = question;
                    break;
                case RuntimeEvent.PlanPresented { PlanId: var planId, FormattedPlan: var plan, RequiresApproval: var needsApproval }:
                    presentedPlan = new SovrantEvent
                    {
                        Event = "plan_presented",
                        PlanId = planId,
                        FormattedPlan = plan,
                        RequiresApproval = needsApproval,
                    };
                    break;
                case RuntimeEvent.TurnCost { EstimatedUsd: var usd, Source: var source }:
                    estimatedUsd = usd;
                    costSource = source;
                    break;
            }
        }

        // Build the Sovrant extension if any Phase 59/55 events were captured.
        SovrantEvent? sovrantExt = null;
        if (clarification is not null)
            sovrantExt = new SovrantEvent { Event = "clarification_needed", Clarification = clarification };
        else if (presentedPlan is not null)
            sovrantExt = presentedPlan;
        else if (estimatedUsd is not null || costSource is not null)
            sovrantExt = new SovrantEvent { Event = "turn_cost", EstimatedUsd = estimatedUsd, CostSource = costSource };

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
            Sovrant = sovrantExt,
        };

        await ctx.Response.WriteAsJsonAsync(response, ct).ConfigureAwait(false);
    }

}
