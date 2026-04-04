using System.Text;
using Sovrant.Api.Routing;
using Sovrant.Runtime.Conversation;
using Sovrant.Server.Permissions;
using Sovrant.Server.ServerConfig;
using Sovrant.Server.Webhooks;
using Sovrant.Tools;

namespace Sovrant.Server.Routes;

/// <summary>
/// Registers <c>POST /v1/webhook</c> — a generic webhook endpoint that accepts a message
/// from any source (Slack, Teams, Discord, custom), runs an agentic turn using a session
/// derived from the source + user ID, and either returns the result synchronously or
/// POSTs it to a callback URL.
/// </summary>
internal static class WebhookRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/webhook", HandleAsync)
            .RequireRateLimiting("per-session");
    }

    private static async Task HandleAsync(
        HttpContext ctx,
        WebhookRequest req,
        IRuntimeSessionPool sessionPool,
        ISmartRouter router,
        MutableServerConfig serverConfig,
        ToolRegistrar toolRegistrar,
        WebhookCallbackService callbackService,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        // ── Validate required fields ────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(req.Source) ||
            string.IsNullOrWhiteSpace(req.UserId) ||
            string.IsNullOrWhiteSpace(req.Message))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(
                new { error = "Fields 'source', 'user_id', and 'message' are required." }, ct)
                .ConfigureAwait(false);
            return;
        }

        // ── Validate callback_url if provided ───────────────────────────────
        if (req.CallbackUrl is not null)
        {
            if (!Uri.TryCreate(req.CallbackUrl, UriKind.Absolute, out var callbackUri) ||
                (callbackUri.Scheme != "https" && callbackUri.Scheme != "http"))
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsJsonAsync(
                    new { error = "callback_url must be an absolute HTTP(S) URL." }, ct)
                    .ConfigureAwait(false);
                return;
            }
        }

        // Seed tools (no-op if already registered).
        toolRegistrar.RegisterAll();

        await router.InitializeAsync(ct).ConfigureAwait(false);

        // ── Derive session ID from source + user_id ─────────────────────────
        var sessionId = $"webhook:{req.Source}:{req.UserId}";

        var pooled = await sessionPool.GetOrCreateAsync(sessionId, ct: ct).ConfigureAwait(false);
        var runtime = pooled.Runtime;
        var sessionLock = pooled.Lock;
        var sessionConfig = pooled.Config;

        var model = req.Model ?? sessionConfig?.Model ?? serverConfig.Model;

        // Apply model override to session config if provided.
        if (req.Model is not null && sessionConfig is not null)
            sessionConfig.Model = req.Model;

        // ── Run the agentic turn ────────────────────────────────────────────
        await sessionLock.WaitAsync(ct).ConfigureAwait(false);
        SessionAwarePermissionModeAdapter.SetCurrent(sessionConfig);

        WebhookResponse response;
        try
        {
            response = await RunTurnAsync(runtime, req, sessionId, model, sessionConfig, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            SessionAwarePermissionModeAdapter.SetCurrent(null);
            sessionLock.Release();
        }

        // ── Deliver result ──────────────────────────────────────────────────
        if (req.CallbackUrl is not null)
        {
            // Async mode: return 202 Accepted immediately, deliver result in background.
            ctx.Response.StatusCode = StatusCodes.Status202Accepted;
            await ctx.Response.WriteAsJsonAsync(
                new { status = "accepted", session_id = sessionId }, ct)
                .ConfigureAwait(false);

            // Fire-and-forget callback delivery (errors are logged, not propagated).
            _ = callbackService.DeliverAsync(req.CallbackUrl, response, CancellationToken.None);
        }
        else
        {
            // Synchronous mode: return the full result inline.
            await ctx.Response.WriteAsJsonAsync(response, ct).ConfigureAwait(false);
        }
    }

    private static async Task<WebhookResponse> RunTurnAsync(
        IConversationRuntime runtime,
        WebhookRequest req,
        string sessionId,
        string model,
        SessionConfig? sessionConfig,
        CancellationToken ct)
    {
        var textBuilder = new StringBuilder();
        var response = new WebhookResponse
        {
            Source = req.Source,
            UserId = req.UserId,
            ThreadId = req.ThreadId,
            SessionId = sessionId,
        };

        await foreach (var ev in runtime.RunTurnAsync(req.Message, ct).ConfigureAwait(false))
        {
            switch (ev)
            {
                case RuntimeEvent.TextChunk { Text: var text }:
                    textBuilder.Append(text);
                    break;

                case RuntimeEvent.ToolResult { ToolUseId: var id, ToolName: var toolName, IsError: var isErr }:
                    response.ToolCalls.Add(new WebhookToolCall { Id = id, ToolName = toolName, IsError = isErr });
                    if (isErr)
                        response.Errors.Add($"{toolName}: error");
                    break;

                case RuntimeEvent.PermissionDenied { ToolName: var toolName, Reason: var reason }:
                    response.Errors.Add($"permission_denied: {toolName}: {reason}");
                    break;

                case RuntimeEvent.TurnComplete { InputTokens: var inp, OutputTokens: var outp }:
                    response.InputTokens = inp;
                    response.OutputTokens = outp;
                    sessionConfig?.AddTokens(inp, outp);
                    break;

                case RuntimeEvent.RuntimeError { Message: var msg }:
                    response.Errors.Add(msg);
                    break;
            }
        }

        response.Success = response.Errors.Count == 0;
        response.Text = textBuilder.ToString();
        return response;
    }
}
