using System.Text.Json.Serialization;
using Sovrant.Runtime.Workspaces;
using Sovrant.Server.Middleware;

namespace Sovrant.Server.Routes;

/// <summary>Registers workspace management endpoints (Phase 35).</summary>
internal static class WorkspaceRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/workspaces", ListWorkspaces);
        app.MapPost("/v1/workspaces", CreateWorkspace);
        app.MapGet("/v1/workspaces/{id}", GetWorkspace);
        app.MapPut("/v1/workspaces/{id}", UpdateWorkspace);
        app.MapDelete("/v1/workspaces/{id}", DeleteWorkspace);

        app.MapGet("/v1/workspaces/{id}/members", ListMembers);
        app.MapPost("/v1/workspaces/{id}/members", AddMember);
        app.MapDelete("/v1/workspaces/{id}/members/{userId}", RemoveMember);

        app.MapPost("/v1/workspaces/{id}/invites", CreateInvite);
        app.MapDelete("/v1/workspaces/{id}/invites/{inviteId}", DeleteInvite);
        app.MapPost("/v1/workspaces/invites/accept", AcceptInvite);

        app.MapGet("/v1/workspaces/{id}/config", GetConfig);
        app.MapPut("/v1/workspaces/{id}/config", PutConfig);

        app.MapGet("/v1/workspaces/{id}/usage", GetUsage);

        app.MapGet("/v1/workspaces/{id}/memory", ListMemory);
        app.MapPost("/v1/workspaces/{id}/memory", SaveMemory);
        app.MapDelete("/v1/workspaces/{id}/memory/{memoryId}", DeleteMemory);
    }

    // ── Workspace CRUD ─────────────────────────────────────────────────────

    private static async Task<IResult> ListWorkspaces(
        HttpContext ctx, IWorkspaceService svc, CancellationToken ct)
    {
        var userId = ctx.GetUserId() ?? string.Empty;
        var workspaces = await svc.ListForUserAsync(userId, ct).ConfigureAwait(false);
        return Results.Ok(new { workspaces });
    }

    private static async Task<IResult> CreateWorkspace(
        CreateWorkspaceRequest req, HttpContext ctx, IWorkspaceService svc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest(new { error = "Name is required." });
        if (string.IsNullOrWhiteSpace(req.Slug))
            return Results.BadRequest(new { error = "Slug is required." });

        var userId = ctx.GetUserId() ?? string.Empty;
        try
        {
            var workspace = await svc.CreateTeamWorkspaceAsync(req.Name, req.Slug, userId, ct).ConfigureAwait(false);
            return Results.Created($"/v1/workspaces/{workspace.WorkspaceId}", workspace);
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Results.Conflict(new { error = "A workspace with that slug already exists." });
        }
    }

    private static async Task<IResult> GetWorkspace(
        string id, IWorkspaceService svc, CancellationToken ct)
    {
        var ws = await svc.GetAsync(id, ct).ConfigureAwait(false);
        return ws is null ? Results.NotFound(new { error = "Workspace not found." }) : Results.Ok(ws);
    }

    private static async Task<IResult> UpdateWorkspace(
        string id, UpdateWorkspaceRequest req, IWorkspaceService svc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var updated = await svc.UpdateAsync(id, req.Name, req.Slug, ct).ConfigureAwait(false);
        return updated is null
            ? Results.NotFound(new { error = "Workspace not found." })
            : Results.Ok(updated);
    }

    private static async Task<IResult> DeleteWorkspace(
        string id, IWorkspaceService svc, CancellationToken ct)
    {
        var deleted = await svc.DeleteAsync(id, ct).ConfigureAwait(false);
        if (!deleted)
            return Results.BadRequest(new { error = "Cannot delete workspace. It may be a personal workspace or not found." });
        return Results.Ok(new { deleted = id });
    }

    // ── Membership ─────────────────────────────────────────────────────────

    private static async Task<IResult> ListMembers(
        string id, IWorkspaceService svc, CancellationToken ct)
    {
        var members = await svc.ListMembersAsync(id, ct).ConfigureAwait(false);
        return Results.Ok(new { members });
    }

    private static async Task<IResult> AddMember(
        string id, AddMemberRequest req, IWorkspaceService svc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        if (string.IsNullOrWhiteSpace(req.UserId))
            return Results.BadRequest(new { error = "user_id is required." });

        var role = WorkspaceRole.Member;
        if (req.Role is not null && !Enum.TryParse(req.Role, ignoreCase: true, out role))
            return Results.BadRequest(new { error = "Invalid role." });

        await svc.AddMemberAsync(id, req.UserId, role, ct).ConfigureAwait(false);
        return Results.Ok(new { added = true });
    }

    private static async Task<IResult> RemoveMember(
        string id, string userId, IWorkspaceService svc, CancellationToken ct)
    {
        var removed = await svc.RemoveMemberAsync(id, userId, ct).ConfigureAwait(false);
        if (!removed)
            return Results.BadRequest(new { error = "Cannot remove member. They may be the owner or not found." });
        return Results.Ok(new { removed = true });
    }

    // ── Invites ────────────────────────────────────────────────────────────

    private static async Task<IResult> CreateInvite(
        string id, CreateInviteRequest req, IWorkspaceService svc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        if (string.IsNullOrWhiteSpace(req.Email))
            return Results.BadRequest(new { error = "Email is required." });

        var role = WorkspaceRole.Member;
        if (req.Role is not null && !Enum.TryParse(req.Role, ignoreCase: true, out role))
            return Results.BadRequest(new { error = "Invalid role." });

        var invite = await svc.CreateInviteAsync(id, req.Email, role, ct).ConfigureAwait(false);
        return Results.Created($"/v1/workspaces/{id}/invites/{invite.InviteId}", invite);
    }

    private static async Task<IResult> DeleteInvite(
        string id, string inviteId, IWorkspaceService svc, CancellationToken ct)
    {
        var deleted = await svc.DeleteInviteAsync(inviteId, ct).ConfigureAwait(false);
        return deleted
            ? Results.Ok(new { deleted = inviteId })
            : Results.NotFound(new { error = "Invite not found." });
    }

    private static async Task<IResult> AcceptInvite(
        AcceptInviteRequest req, HttpContext ctx, IWorkspaceService svc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        if (string.IsNullOrWhiteSpace(req.Token))
            return Results.BadRequest(new { error = "Token is required." });

        var userId = ctx.GetUserId() ?? string.Empty;
        var accepted = await svc.AcceptInviteAsync(req.Token, userId, ct).ConfigureAwait(false);
        return accepted
            ? Results.Ok(new { accepted = true })
            : Results.BadRequest(new { error = "Invalid, expired, or already accepted invite." });
    }

    // ── Config ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetConfig(
        string id, IWorkspaceService svc, CancellationToken ct)
    {
        var config = await svc.GetConfigAsync(id, ct).ConfigureAwait(false);
        return Results.Ok(new { config });
    }

    private static async Task<IResult> PutConfig(
        string id, Dictionary<string, string> values, IWorkspaceService svc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(values);
        await svc.SetConfigAsync(id, values, ct).ConfigureAwait(false);
        return Results.Ok(new { updated = true });
    }

    // ── Usage ──────────────────────────────────────────────────────────────

    private static async Task<IResult> GetUsage(
        string id, IWorkspaceService svc, CancellationToken ct)
    {
        var usage = await svc.GetUsageAsync(id, ct).ConfigureAwait(false);
        return Results.Ok(usage);
    }

    // ── Memory ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ListMemory(
        string id, string? layer, IWorkspaceService svc, CancellationToken ct)
    {
        var memory = await svc.ListMemoryAsync(id, layer, ct).ConfigureAwait(false);
        return Results.Ok(new { memory });
    }

    private static async Task<IResult> SaveMemory(
        string id, SaveWorkspaceMemoryRequest req, IWorkspaceService svc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        if (string.IsNullOrWhiteSpace(req.Layer))
            return Results.BadRequest(new { error = "Layer is required." });
        if (string.IsNullOrWhiteSpace(req.Content))
            return Results.BadRequest(new { error = "Content is required." });

        var entry = new WorkspaceMemoryEntry
        {
            MemoryId = req.MemoryId ?? $"mem-{Guid.NewGuid():N}",
            WorkspaceId = id,
            Layer = req.Layer,
            Content = req.Content,
            Confidence = req.Confidence,
            ProjectId = req.ProjectId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await svc.SaveMemoryAsync(entry, ct).ConfigureAwait(false);
        return Results.Created($"/v1/workspaces/{id}/memory/{entry.MemoryId}", entry);
    }

    private static async Task<IResult> DeleteMemory(
        string id, string memoryId, IWorkspaceService svc, CancellationToken ct)
    {
        var deleted = await svc.DeleteMemoryAsync(memoryId, ct).ConfigureAwait(false);
        return deleted
            ? Results.Ok(new { deleted = memoryId })
            : Results.NotFound(new { error = "Memory entry not found." });
    }
}

// ── Request DTOs ───────────────────────────────────────────────────────────

internal sealed class CreateWorkspaceRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }
}

internal sealed class UpdateWorkspaceRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }
}

internal sealed class AddMemberRequest
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }
}

internal sealed class CreateInviteRequest
{
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }
}

internal sealed class AcceptInviteRequest
{
    [JsonPropertyName("token")]
    public string? Token { get; init; }
}

internal sealed class SaveWorkspaceMemoryRequest
{
    [JsonPropertyName("memory_id")]
    public string? MemoryId { get; init; }

    [JsonPropertyName("layer")]
    public string? Layer { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("confidence")]
    public double? Confidence { get; init; }

    [JsonPropertyName("project_id")]
    public string? ProjectId { get; init; }
}
