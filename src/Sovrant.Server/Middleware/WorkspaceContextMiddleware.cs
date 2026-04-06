using Sovrant.Runtime.Workspaces;

namespace Sovrant.Server.Middleware;

/// <summary>
/// Resolves the active workspace from the <c>X-Workspace-Id</c> header.
/// Falls back to the authenticated user's personal workspace when the header is absent.
/// Stores the resolved workspace ID in <see cref="HttpContext.Items"/> under <c>"WorkspaceId"</c>.
/// </summary>
internal sealed class WorkspaceContextMiddleware(RequestDelegate next)
{
    /// <summary>Key used to store the resolved workspace ID in HttpContext.Items.</summary>
    public const string WorkspaceIdKey = "WorkspaceId";

    /// <summary>Key used to store the resolved user ID in HttpContext.Items.</summary>
    public const string UserIdKey = "UserId";

    public async Task InvokeAsync(HttpContext context, IWorkspaceService workspaceService)
    {
        // Skip workspace resolution for health check and OPTIONS preflight.
        if (context.Request.Method == HttpMethods.Options ||
            context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Resolve user — for now use the SOVRANT_USER_ID env var or system username.
        // Phase 40 will replace this with proper IdP-backed resolution.
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        context.Items[UserIdKey] = userId;

        // Try explicit workspace from header.
        var workspaceId = context.Request.Headers["X-Workspace-Id"].FirstOrDefault();

        if (!string.IsNullOrEmpty(workspaceId))
        {
            // Validate membership.
            var isMember = await workspaceService.IsMemberAsync(workspaceId, userId).ConfigureAwait(false);
            if (!isMember)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Not a member of the specified workspace." }).ConfigureAwait(false);
                return;
            }

            context.Items[WorkspaceIdKey] = workspaceId;
        }
        else
        {
            // Fallback: resolve to user's personal workspace.
            var personal = await workspaceService.GetPersonalAsync(userId).ConfigureAwait(false);
            if (personal is not null)
                context.Items[WorkspaceIdKey] = personal.WorkspaceId;
            // If no personal workspace yet (race condition at startup), proceed without.
        }

        await next(context).ConfigureAwait(false);
    }
}

/// <summary>Extension methods for reading workspace context from HttpContext.</summary>
internal static class WorkspaceContextExtensions
{
    /// <summary>Gets the resolved workspace ID from the current request, or null.</summary>
    public static string? GetWorkspaceId(this HttpContext context) =>
        context.Items.TryGetValue(WorkspaceContextMiddleware.WorkspaceIdKey, out var v) ? v as string : null;

    /// <summary>Gets the resolved user ID from the current request, or null.</summary>
    public static string? GetUserId(this HttpContext context) =>
        context.Items.TryGetValue(WorkspaceContextMiddleware.UserIdKey, out var v) ? v as string : null;
}
