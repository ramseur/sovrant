using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Workspaces;
using Sovrant.Server.Auth;

namespace Sovrant.Server.Routes;

/// <summary>
/// Phase 53 — artifact listing and download endpoints.
/// <list type="bullet">
///   <item><c>GET /v1/artifacts</c> — list artifacts filtered by scope</item>
///   <item><c>GET /v1/artifacts/{runId}/{**path}</c> — download a single artifact</item>
///   <item><c>DELETE /v1/artifacts/{runId}</c> — delete all artifacts for a run</item>
/// </list>
/// </summary>
internal static class ArtifactRoutes
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static void Map(WebApplication app)
    {
        // ── List artifacts ──────────────────────────────────────────────

        app.MapGet("/v1/artifacts", async (
            HttpContext ctx,
            IArtifactStore store,
            IWorkspaceService wsSvc,
            CancellationToken ct) =>
        {
            var scope = ScopeFromQuery(ctx);
            var deny = await WorkspaceAuthGuards.RequireWorkspaceAccessAsync(ctx, scope.WorkspaceId, wsSvc, ct).ConfigureAwait(false);
            if (deny is not null) return deny;

            var entries = new List<object>();
            await foreach (var entry in store.ListAsync(scope, ctx.RequestAborted))
            {
                entries.Add(new
                {
                    relative_path = entry.RelativePath,
                    size_bytes = entry.SizeBytes,
                    content_type = entry.ContentType,
                    last_modified = entry.LastModified,
                    run_id = entry.RunId,
                });
            }

            return Results.Json(new { artifacts = entries, count = entries.Count }, s_jsonOptions);
        });

        // ── Download a single artifact ──────────────────────────────────

        app.MapGet("/v1/artifacts/{runId}/{**path}", async (
            string runId,
            string path,
            HttpContext ctx,
            IArtifactStore store,
            IWorkspaceService wsSvc,
            CancellationToken ct) =>
        {
            if (!IsValidArtifactPath(path))
                return Results.BadRequest(new { error = "Invalid artifact path." });

            var scope = ScopeFromQuery(ctx, runId);
            var deny = await WorkspaceAuthGuards.RequireWorkspaceAccessAsync(ctx, scope.WorkspaceId, wsSvc, ct).ConfigureAwait(false);
            if (deny is not null) return deny;

            ArtifactHandle handle;
            try
            {
                handle = await store.CreateRunScopeAsync(scope, ct);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            try
            {
                var stream = await store.ReadAsync(handle, path, ct);
                var contentType = MimeTypeFromExtension(path);
                return Results.Stream(stream, contentType, Path.GetFileName(path));
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound(new { error = $"Artifact not found: {path}" });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // ── Delete a run's artifacts ────────────────────────────────────

        app.MapDelete("/v1/artifacts/{runId}", async (
            string runId,
            HttpContext ctx,
            IArtifactStore store,
            IWorkspaceService wsSvc,
            CancellationToken ct) =>
        {
            var scope = ScopeFromQuery(ctx, runId);
            var deny = await WorkspaceAuthGuards.RequireWorkspaceAccessAsync(ctx, scope.WorkspaceId, wsSvc, ct).ConfigureAwait(false);
            if (deny is not null) return deny;

            try
            {
                await store.DeleteAsync(scope, ct);
                return Results.Ok(new { deleted = true, run_id = runId });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }

    // Workspace access guard lives in Sovrant.Server.Auth.WorkspaceAuthGuards.

    /// <summary>
    /// Rejects paths containing traversal sequences or null bytes.
    /// The store's ResolveAndGuard is the enforcement layer; this is an
    /// explicit route-level gate so bad input is rejected before any disk I/O.
    /// </summary>
    private static bool IsValidArtifactPath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && !path.Contains("..", StringComparison.Ordinal)
        && path.IndexOf('\0', StringComparison.Ordinal) < 0;

    /// <summary>
    /// Builds an <see cref="ArtifactScope"/> from query-string parameters and
    /// optional route values.
    /// </summary>
    private static ArtifactScope ScopeFromQuery(HttpContext ctx, string? runId = null)
    {
        var query = ctx.Request.Query;
        return new ArtifactScope
        {
            WorkspaceId = query["workspace_id"].FirstOrDefault()
                ?? ctx.Request.Headers["X-Workspace-Id"].FirstOrDefault()
                ?? WorkspaceIdentity.DefaultPersonal(),
            ProjectId = query["project_id"].FirstOrDefault()
                ?? ctx.Request.Headers["X-Project-Id"].FirstOrDefault()
                ?? ArtifactScope.DefaultProjectId,
            RunId = runId ?? query["run_id"].FirstOrDefault(),
        };
    }

    private static string MimeTypeFromExtension(string path)
    {
        var ext = Path.GetExtension(path);
        return ext switch
        {
            ".json" => "application/json",
            ".md" => "text/markdown",
            ".txt" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".csv" => "text/csv",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            _ => "application/octet-stream",
        };
    }
}
