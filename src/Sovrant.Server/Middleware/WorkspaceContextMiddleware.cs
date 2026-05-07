using Sovrant.Runtime.Workspaces;
using Sovrant.Server.Auth;

namespace Sovrant.Server.Middleware;

/// <summary>
/// Resolves the active workspace from the <c>X-Workspace-Id</c> header (or falls back to the
/// authenticated user's personal workspace) and stores it in <see cref="HttpContext.Items"/>
/// under <see cref="SovrantHttpContextKeys.WorkspaceId"/>.
///
/// Must run after <see cref="BearerTokenMiddleware"/> so that the authenticated user ID is
/// already available in <c>HttpContext.Items[SovrantHttpContextKeys.UserId]</c>.
/// </summary>
internal sealed class WorkspaceContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IWorkspaceService workspaceService)
    {
        // Skip workspace resolution for health check and OPTIONS preflight.
        if (context.Request.Method == HttpMethods.Options ||
            context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Read the authenticated user from BearerTokenMiddleware — never fall back to env vars.
        var userId = HttpContextAuthExtensions.GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            // Unauthenticated — let the route handler return 401.
            await next(context).ConfigureAwait(false);
            return;
        }

        // Try explicit workspace from header.
        var workspaceId = context.Request.Headers["X-Workspace-Id"].FirstOrDefault();

        if (!string.IsNullOrEmpty(workspaceId))
        {
            // Admins bypass membership checks; everyone else must be a member.
            var isAdmin = HttpContextAuthExtensions.IsAdmin(context);
            if (!isAdmin)
            {
                var isMember = await workspaceService.IsMemberAsync(workspaceId, userId).ConfigureAwait(false);
                if (!isMember)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "Not a member of the specified workspace." }).ConfigureAwait(false);
                    return;
                }
            }

            context.Items[SovrantHttpContextKeys.WorkspaceId] = workspaceId;
        }
        else
        {
            // Fallback: resolve to the authenticated user's personal workspace.
            var personal = await workspaceService.GetPersonalAsync(userId).ConfigureAwait(false);
            if (personal is not null)
                context.Items[SovrantHttpContextKeys.WorkspaceId] = personal.WorkspaceId;
            // If no personal workspace yet (race at first boot), proceed without — the route will handle it.
        }

        await next(context).ConfigureAwait(false);
    }
}

/// <summary>Extension methods for reading workspace context from <see cref="HttpContext"/>.</summary>
internal static class WorkspaceContextExtensions
{
    /// <summary>Gets the resolved workspace ID from the current request, or null.</summary>
    public static string? GetWorkspaceId(this HttpContext context) =>
        context.Items.TryGetValue(SovrantHttpContextKeys.WorkspaceId, out var v) ? v as string : null;
}
