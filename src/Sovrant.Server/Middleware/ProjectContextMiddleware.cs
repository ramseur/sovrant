using Sovrant.Runtime.Projects;
using Sovrant.Server.Auth;

namespace Sovrant.Server.Middleware;

/// <summary>
/// Resolves the active project from the <c>X-Project-Id</c> header.
/// Validates that the user has access to the project (workspace member + open or explicit project member).
/// Stores the resolved project ID in <see cref="HttpContext.Items"/> under <c>"ProjectId"</c>.
/// </summary>
internal sealed class ProjectContextMiddleware(RequestDelegate next)
{
    /// <summary>Key used to store the resolved project ID in HttpContext.Items.</summary>
    public const string ProjectIdKey = "ProjectId";

    public async Task InvokeAsync(HttpContext context, IProjectService projectService)
    {
        // Skip for health check and OPTIONS preflight.
        if (context.Request.Method == HttpMethods.Options ||
            context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var projectId = context.Request.Headers["X-Project-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(projectId))
        {
            var userId = HttpContextAuthExtensions.GetUserId(context) ?? string.Empty;
            var hasAccess = await projectService.HasAccessAsync(projectId, userId).ConfigureAwait(false);
            if (!hasAccess)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "No access to the specified project." }).ConfigureAwait(false);
                return;
            }

            context.Items[ProjectIdKey] = projectId;
        }

        await next(context).ConfigureAwait(false);
    }
}

/// <summary>Extension methods for reading project context from HttpContext.</summary>
internal static class ProjectContextExtensions
{
    /// <summary>Gets the resolved project ID from the current request, or null.</summary>
    public static string? GetProjectId(this HttpContext context) =>
        context.Items.TryGetValue(ProjectContextMiddleware.ProjectIdKey, out var v) ? v as string : null;
}
