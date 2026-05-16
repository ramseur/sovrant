using System.Text.Json.Serialization;
using Sovrant.Runtime.Projects;
using Sovrant.Runtime.Workspaces;
using Sovrant.Server.Auth;
using Sovrant.Server.Middleware;

// Phase G (8.2) — added workspace-level auth checks to ListProjects and CreateProject.

namespace Sovrant.Server.Routes;

/// <summary>Registers project management endpoints (Phase 36).</summary>
internal static class ProjectRoutes
{
    public static void Map(WebApplication app)
    {
        // Workspace-scoped project list/create.
        app.MapGet("/v1/workspaces/{wid}/projects", ListProjects);
        app.MapPost("/v1/workspaces/{wid}/projects", CreateProject);

        // Project CRUD.
        app.MapGet("/v1/projects/{id}", GetProject);
        app.MapPut("/v1/projects/{id}", UpdateProject);
        app.MapDelete("/v1/projects/{id}", DeleteProject);
        app.MapPost("/v1/projects/{id}/archive", ArchiveProject);
        app.MapPost("/v1/projects/{id}/unarchive", UnarchiveProject);

        // Membership.
        app.MapGet("/v1/projects/{id}/members", ListMembers);
        app.MapPost("/v1/projects/{id}/members", AddMember);
        app.MapDelete("/v1/projects/{id}/members/{userId}", RemoveMember);

        // Config.
        app.MapGet("/v1/projects/{id}/config", GetConfig);
        app.MapPut("/v1/projects/{id}/config", PutConfig);

        // Sessions, usage, memory.
        app.MapGet("/v1/projects/{id}/sessions", ListSessions);
        app.MapGet("/v1/projects/{id}/usage", GetUsage);
        app.MapGet("/v1/projects/{id}/memory", GetMemory);
    }

    // ── Workspace-scoped ───────────────────────────────────────────────────

    private static async Task<IResult> ListProjects(
        HttpContext ctx, string wid, bool? includeArchived,
        IProjectService svc, IWorkspaceService wsSvc, CancellationToken ct)
    {
        if (!InputValidation.IsValidResourceId(wid))
            return Results.BadRequest(new { error = "Invalid workspace ID format." });
        var deny = await WorkspaceAuthGuards.RequireWorkspaceAccessAsync(ctx, wid, wsSvc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;
        var projects = await svc.ListAsync(wid, includeArchived ?? false, ct).ConfigureAwait(false);
        return Results.Ok(new { projects });
    }

    private static async Task<IResult> CreateProject(
        HttpContext ctx, string wid, CreateProjectRequest req,
        IProjectService svc, IWorkspaceService wsSvc, CancellationToken ct)
    {
        if (!InputValidation.IsValidResourceId(wid))
            return Results.BadRequest(new { error = "Invalid workspace ID format." });
        var deny = await WorkspaceAuthGuards.RequireWorkspaceAccessAsync(ctx, wid, wsSvc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;
        ArgumentNullException.ThrowIfNull(req);
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest(new { error = "Name is required." });
        if (string.IsNullOrWhiteSpace(req.Slug))
            return Results.BadRequest(new { error = "Slug is required." });
        if (!InputValidation.IsValidSlug(req.Slug))
            return Results.BadRequest(new { error = "Slug may only contain letters, digits, hyphens, and underscores (max 80 chars)." });

        try
        {
            var project = await svc.CreateAsync(wid, req.Name, req.Slug, req.Description, ct).ConfigureAwait(false);
            return Results.Created($"/v1/projects/{project.ProjectId}", project);
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Results.Conflict(new { error = "A project with that slug already exists in this workspace." });
        }
    }

    // ── Auth helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the caller has access to the given project. Admins always pass;
    /// non-admin callers must be a project member (or have open access).
    /// </summary>
    private static async Task<IResult?> RequireProjectAccess(
        HttpContext ctx, string projectId, IProjectService svc, CancellationToken ct)
    {
        if (ctx.IsAdmin()) return null; // admin — always allowed
        var userId = HttpContextAuthExtensions.GetUserId(ctx) ?? string.Empty;
        if (!await svc.HasAccessAsync(projectId, userId, ct).ConfigureAwait(false))
            return Results.Json(new { error = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);
        return null;
    }

    // Workspace access/manage guards live in Sovrant.Server.Auth.WorkspaceAuthGuards.

    // ── Project CRUD ───────────────────────────────────────────────────────

    private static async Task<IResult> GetProject(
        HttpContext ctx, string id, IProjectService svc, CancellationToken ct)
    {
        var deny = await RequireProjectAccess(ctx, id, svc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;

        var project = await svc.GetAsync(id, ct).ConfigureAwait(false);
        return project is null ? Results.NotFound(new { error = "Project not found." }) : Results.Ok(project);
    }

    private static async Task<IResult> UpdateProject(
        HttpContext ctx, string id, UpdateProjectRequest req, IProjectService svc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var deny = await RequireProjectAccess(ctx, id, svc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;

        var updated = await svc.UpdateAsync(id, req.Name, req.Slug, req.Description, ct).ConfigureAwait(false);
        return updated is null
            ? Results.NotFound(new { error = "Project not found." })
            : Results.Ok(updated);
    }

    private static async Task<IResult> DeleteProject(
        HttpContext ctx, string id, IProjectService svc, IWorkspaceService wsSvc, CancellationToken ct)
    {
        // Resolve workspace for this project to check workspace-level manage role.
        var project = await svc.GetAsync(id, ct).ConfigureAwait(false);
        if (project is null) return Results.NotFound(new { error = "Project not found." });
        var deny = await WorkspaceAuthGuards.RequireWorkspaceManageAsync(ctx, project.WorkspaceId, wsSvc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;

        var deleted = await svc.DeleteAsync(id, ct).ConfigureAwait(false);
        return deleted
            ? Results.Ok(new { deleted = id })
            : Results.NotFound(new { error = "Project not found." });
    }

    private static async Task<IResult> ArchiveProject(
        HttpContext ctx, string id, IProjectService svc, CancellationToken ct)
    {
        var deny = await RequireProjectAccess(ctx, id, svc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;

        var archived = await svc.ArchiveAsync(id, ct).ConfigureAwait(false);
        return archived
            ? Results.Ok(new { archived = id })
            : Results.BadRequest(new { error = "Project not found or already archived." });
    }

    private static async Task<IResult> UnarchiveProject(
        HttpContext ctx, string id, IProjectService svc, CancellationToken ct)
    {
        var deny = await RequireProjectAccess(ctx, id, svc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;

        var unarchived = await svc.UnarchiveAsync(id, ct).ConfigureAwait(false);
        return unarchived
            ? Results.Ok(new { unarchived = id })
            : Results.BadRequest(new { error = "Project not found or not archived." });
    }

    // ── Membership ─────────────────────────────────────────────────────────

    private static async Task<IResult> ListMembers(
        HttpContext ctx, string id, IProjectService svc, CancellationToken ct)
    {
        var deny = await RequireProjectAccess(ctx, id, svc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;

        var members = await svc.ListMembersAsync(id, ct).ConfigureAwait(false);
        return Results.Ok(new { members });
    }

    private static async Task<IResult> AddMember(
        HttpContext ctx, string id, AddProjectMemberRequest req, IProjectService svc, IWorkspaceService wsSvc, CancellationToken ct)
    {
        var project = await svc.GetAsync(id, ct).ConfigureAwait(false);
        if (project is null) return Results.NotFound(new { error = "Project not found." });
        var deny = await WorkspaceAuthGuards.RequireWorkspaceManageAsync(ctx, project.WorkspaceId, wsSvc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;

        ArgumentNullException.ThrowIfNull(req);
        if (string.IsNullOrWhiteSpace(req.UserId))
            return Results.BadRequest(new { error = "user_id is required." });

        var role = ProjectRole.Contributor;
        if (req.Role is not null && !Enum.TryParse(req.Role, ignoreCase: true, out role))
            return Results.BadRequest(new { error = "Invalid role. Must be lead, contributor, or viewer." });

        var added = await svc.AddMemberAsync(id, req.UserId, role, ct).ConfigureAwait(false);
        return added
            ? Results.Ok(new { added = true })
            : Results.BadRequest(new { error = "Cannot add member. User may not be a workspace member or project not found." });
    }

    private static async Task<IResult> RemoveMember(
        HttpContext ctx, string id, string userId, IProjectService svc, IWorkspaceService wsSvc, CancellationToken ct)
    {
        if (!InputValidation.IsValidResourceId(userId))
            return Results.BadRequest(new { error = "Invalid user ID format." });
        var project = await svc.GetAsync(id, ct).ConfigureAwait(false);
        if (project is null) return Results.NotFound(new { error = "Project not found." });
        var deny = await WorkspaceAuthGuards.RequireWorkspaceManageAsync(ctx, project.WorkspaceId, wsSvc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;

        var removed = await svc.RemoveMemberAsync(id, userId, ct).ConfigureAwait(false);
        return removed
            ? Results.Ok(new { removed = true })
            : Results.NotFound(new { error = "Member not found." });
    }

    // ── Config ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetConfig(
        HttpContext ctx, string id, bool? resolved, IProjectService svc, CancellationToken ct)
    {
        var deny = await RequireProjectAccess(ctx, id, svc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;

        var config = (resolved ?? false)
            ? await svc.GetResolvedConfigAsync(id, ct).ConfigureAwait(false)
            : await svc.GetConfigAsync(id, ct).ConfigureAwait(false);
        return Results.Ok(new { config });
    }

    private static async Task<IResult> PutConfig(
        HttpContext ctx, string id, Dictionary<string, string> values, IProjectService svc, CancellationToken ct)
    {
        var deny = await RequireProjectAccess(ctx, id, svc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;

        ArgumentNullException.ThrowIfNull(values);
        await svc.SetConfigAsync(id, values, ct).ConfigureAwait(false);
        return Results.Ok(new { updated = true });
    }

    // ── Sessions, Usage, Memory ────────────────────────────────────────────

    private static async Task<IResult> ListSessions(
        HttpContext ctx, string id, IProjectService svc, CancellationToken ct)
    {
        var deny = await RequireProjectAccess(ctx, id, svc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;

        var sessions = await svc.ListSessionsAsync(id, ct).ConfigureAwait(false);
        return Results.Ok(new { sessions });
    }

    private static async Task<IResult> GetUsage(
        HttpContext ctx, string id, IProjectService svc, CancellationToken ct)
    {
        var deny = await RequireProjectAccess(ctx, id, svc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;

        var usage = await svc.GetUsageAsync(id, ct).ConfigureAwait(false);
        return Results.Ok(usage);
    }

    private static async Task<IResult> GetMemory(
        HttpContext ctx, string id, string? layer, IProjectService svc, CancellationToken ct)
    {
        var deny = await RequireProjectAccess(ctx, id, svc, ct).ConfigureAwait(false);
        if (deny is not null) return deny;

        var memory = await svc.GetMemoryAsync(id, layer, ct).ConfigureAwait(false);
        return Results.Ok(new { memory });
    }
}

// ── Request DTOs ───────────────────────────────────────────────────────────

internal sealed class CreateProjectRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

internal sealed class UpdateProjectRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

internal sealed class AddProjectMemberRequest
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }
}
