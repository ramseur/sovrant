using Sovrant.Runtime.Workspaces;

namespace Sovrant.Server.Auth;

/// <summary>
/// Shared authorization guards for workspace-scoped routes. Returns a 403
/// <see cref="IResult"/> when access is denied, or <see langword="null"/>
/// when the caller is allowed to proceed.
///
/// <para>Used by <c>WorkspaceRoutes</c>, <c>ProjectRoutes</c>, and
/// <c>ArtifactRoutes</c>. Previously each of those files defined its own
/// near-identical copies of these checks.</para>
/// </summary>
internal static class WorkspaceAuthGuards
{
    /// <summary>Any workspace member (or global admin) may read.</summary>
    public static async Task<IResult?> RequireWorkspaceAccessAsync(
        HttpContext ctx, string workspaceId, IWorkspaceService svc, CancellationToken ct)
    {
        if (ctx.IsAdmin()) return null;
        var userId = ctx.GetUserId() ?? string.Empty;
        if (!await svc.IsMemberAsync(workspaceId, userId, ct).ConfigureAwait(false))
            return Results.Json(new { error = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);
        return null;
    }

    /// <summary>Workspace owner, workspace admin, or global admin may manage members/config.</summary>
    public static async Task<IResult?> RequireWorkspaceManageAsync(
        HttpContext ctx, string workspaceId, IWorkspaceService svc, CancellationToken ct)
    {
        if (ctx.IsAdmin()) return null;
        var userId = ctx.GetUserId() ?? string.Empty;
        var role = await svc.GetMemberRoleAsync(workspaceId, userId, ct).ConfigureAwait(false);
        if (role is null || !role.Value.IsAtLeast(WorkspaceRole.Admin))
            return Results.Json(new { error = "Workspace admin role required." }, statusCode: StatusCodes.Status403Forbidden);
        return null;
    }

    /// <summary>Only the workspace owner or global admin may delete the workspace.</summary>
    public static async Task<IResult?> RequireWorkspaceOwnerAsync(
        HttpContext ctx, string workspaceId, IWorkspaceService svc, CancellationToken ct)
    {
        if (ctx.IsAdmin()) return null;
        var userId = ctx.GetUserId() ?? string.Empty;
        var role = await svc.GetMemberRoleAsync(workspaceId, userId, ct).ConfigureAwait(false);
        if (role is null || role.Value != WorkspaceRole.Owner)
            return Results.Json(new { error = "Workspace owner role required." }, statusCode: StatusCodes.Status403Forbidden);
        return null;
    }
}
