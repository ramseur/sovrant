using System.Text.Json.Serialization;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Workflows;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;
using Sovrant.Server.Auth;

namespace Sovrant.Server.Routes;

/// <summary>
/// Phase 99 — privacy-toggle endpoints. The owning user can flip
/// <c>is_private</c> on a session, mission, or agent run they own. Admins
/// do NOT get an override here: privacy is a personal control, not a
/// moderation control, so the route requires strict ownership.
/// </summary>
internal static class PrivacyRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapPatch("/v1/sessions/{id}/privacy", UpdateSessionPrivacy);
        app.MapPatch("/v1/workflows/{id}/privacy", UpdateWorkflowPrivacy);
        app.MapPatch("/v1/agent-runs/{id}/privacy", UpdateAgentRunPrivacy);
    }

    private static async Task<IResult> UpdateSessionPrivacy(
        string id,
        PrivacyUpdateRequest req,
        HttpContext ctx,
        ISessionStore store,
        IAuditStore audit,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        var me = ctx.GetUserId();
        if (me is null) return Results.Unauthorized();

        var owner = await store.GetOwnerAsync(id, ct).ConfigureAwait(false);
        if (owner is null) return Results.NotFound(new { error = $"Session '{id}' not found." });
        if (!string.Equals(owner, me, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        await store.UpdatePrivacyAsync(id, me, req.IsPrivate, ct).ConfigureAwait(false);
        await audit.LogPrivacyChangeAsync(me, "session", id, req.IsPrivate, ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateWorkflowPrivacy(
        string id,
        PrivacyUpdateRequest req,
        HttpContext ctx,
        IWorkflowStore store,
        IAuditStore audit,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        var me = ctx.GetUserId();
        if (me is null) return Results.Unauthorized();

        var mission = await store.GetAsync(id, ct).ConfigureAwait(false);
        if (mission is null) return Results.NotFound(new { error = $"Workflow '{id}' not found." });
        if (!string.Equals(mission.OwnerUserId, me, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        await store.UpdatePrivacyAsync(id, me, req.IsPrivate, ct).ConfigureAwait(false);
        await audit.LogPrivacyChangeAsync(me, "workflow", id, req.IsPrivate, ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateAgentRunPrivacy(
        string id,
        PrivacyUpdateRequest req,
        HttpContext ctx,
        IAgentRunStore store,
        IAuditStore audit,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        var me = ctx.GetUserId();
        if (me is null) return Results.Unauthorized();

        var run = await store.GetAsync(id, ct).ConfigureAwait(false);
        if (run is null) return Results.NotFound(new { error = $"Agent run '{id}' not found." });
        if (!string.Equals(run.UserId, me, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        await store.UpdatePrivacyAsync(id, me, req.IsPrivate, ct).ConfigureAwait(false);
        await audit.LogPrivacyChangeAsync(me, "agent_run", id, req.IsPrivate, ct).ConfigureAwait(false);
        return Results.NoContent();
    }
}

internal sealed class PrivacyUpdateRequest
{
    [JsonPropertyName("isPrivate")]
    public bool IsPrivate { get; init; }
}
