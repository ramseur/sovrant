using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Workspaces;
using Sovrant.Server.Auth;

namespace Sovrant.Server.Routes;

/// <summary>
/// CRUD endpoints for MCP trust gate rules.
/// Workspace owners and admins may write; members may read.
/// </summary>
internal static class McpTrustRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/workspaces/{workspaceId}/mcp-trust-rules", ListAsync);
        app.MapPost("/v1/workspaces/{workspaceId}/mcp-trust-rules", CreateAsync);
        app.MapPut("/v1/workspaces/{workspaceId}/mcp-trust-rules/{ruleId}", UpdateAsync);
        app.MapDelete("/v1/workspaces/{workspaceId}/mcp-trust-rules/{ruleId}", DeleteAsync);
    }

    private static async Task<IResult> ListAsync(
        string workspaceId,
        string? serverName,
        IMcpTrustRuleStore store,
        IWorkspaceService workspaceSvc,
        HttpContext ctx,
        CancellationToken ct)
    {
        var deny = await WorkspaceAuthGuards.RequireWorkspaceAccessAsync(ctx, workspaceId, workspaceSvc, ct)
            .ConfigureAwait(false);
        if (deny is not null) return deny;

        var rules = await store.GetRulesAsync(workspaceId, serverName, ct).ConfigureAwait(false);
        return Results.Ok(rules.Select(r => ToDto(r)));
    }

    private static async Task<IResult> CreateAsync(
        string workspaceId,
        McpTrustRuleRequest req,
        IMcpTrustRuleStore store,
        IWorkspaceService workspaceSvc,
        HttpContext ctx,
        CancellationToken ct)
    {
        var deny = await WorkspaceAuthGuards.RequireWorkspaceManageAsync(ctx, workspaceId, workspaceSvc, ct)
            .ConfigureAwait(false);
        if (deny is not null) return deny;

        if (!Enum.TryParse<McpTrustAction>(req.Action, ignoreCase: true, out var action))
            return Results.BadRequest(new { error = $"Invalid action '{req.Action}'. Valid values: Allow, RequireConfirmation, Block." });

        if (string.IsNullOrWhiteSpace(req.ServerName) || string.IsNullOrWhiteSpace(req.ToolPattern))
            return Results.BadRequest(new { error = "server_name and tool_pattern are required." });

        var now = DateTimeOffset.UtcNow;
        var rule = new McpTrustRule(
            RuleId: Guid.NewGuid().ToString("N"),
            WorkspaceId: workspaceId,
            ServerName: req.ServerName.Trim(),
            ToolPattern: req.ToolPattern.Trim(),
            Action: action,
            Reason: string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim(),
            CreatedAt: now,
            UpdatedAt: now);

        await store.UpsertAsync(rule, ct).ConfigureAwait(false);
        return Results.Created(
            new Uri($"/v1/workspaces/{workspaceId}/mcp-trust-rules/{rule.RuleId}", UriKind.Relative),
            ToDto(rule));
    }

    private static async Task<IResult> UpdateAsync(
        string workspaceId,
        string ruleId,
        McpTrustRuleRequest req,
        IMcpTrustRuleStore store,
        IWorkspaceService workspaceSvc,
        HttpContext ctx,
        CancellationToken ct)
    {
        var deny = await WorkspaceAuthGuards.RequireWorkspaceManageAsync(ctx, workspaceId, workspaceSvc, ct)
            .ConfigureAwait(false);
        if (deny is not null) return deny;

        var existing = await store.GetByIdAsync(ruleId, workspaceId, ct).ConfigureAwait(false);
        if (existing is null)
            return Results.NotFound(new { error = $"Rule '{ruleId}' not found." });

        if (!Enum.TryParse<McpTrustAction>(req.Action, ignoreCase: true, out var action))
            return Results.BadRequest(new { error = $"Invalid action '{req.Action}'." });

        var updated = existing with
        {
            ServerName = string.IsNullOrWhiteSpace(req.ServerName) ? existing.ServerName : req.ServerName.Trim(),
            ToolPattern = string.IsNullOrWhiteSpace(req.ToolPattern) ? existing.ToolPattern : req.ToolPattern.Trim(),
            Action = action,
            Reason = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await store.UpsertAsync(updated, ct).ConfigureAwait(false);
        return Results.Ok(ToDto(updated));
    }

    private static async Task<IResult> DeleteAsync(
        string workspaceId,
        string ruleId,
        IMcpTrustRuleStore store,
        IWorkspaceService workspaceSvc,
        HttpContext ctx,
        CancellationToken ct)
    {
        var deny = await WorkspaceAuthGuards.RequireWorkspaceManageAsync(ctx, workspaceId, workspaceSvc, ct)
            .ConfigureAwait(false);
        if (deny is not null) return deny;

        await store.DeleteAsync(ruleId, workspaceId, ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static object ToDto(McpTrustRule r) => new
    {
        rule_id = r.RuleId,
        workspace_id = r.WorkspaceId,
        server_name = r.ServerName,
        tool_pattern = r.ToolPattern,
        action = r.Action.ToString(),
        reason = r.Reason,
        created_at = r.CreatedAt,
        updated_at = r.UpdatedAt,
    };

    private sealed record McpTrustRuleRequest(
        string ServerName,
        string ToolPattern,
        string Action,
        string? Reason);
}
