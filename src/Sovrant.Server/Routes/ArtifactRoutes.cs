using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Projects;
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
            IProjectService projSvc,
            CancellationToken ct) =>
        {
            var scope = await ScopeFromQueryAsync(ctx, wsSvc, projSvc, ct);
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
            IProjectService projSvc,
            CancellationToken ct) =>
        {
            if (!IsValidArtifactPath(path))
                return Results.BadRequest(new { error = "Invalid artifact path." });

            var scope = await ScopeFromQueryAsync(ctx, wsSvc, projSvc, ct, runId);
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
                ApplyArtifactSecurityHeaders(ctx);
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

        // ── Zip download — entire run as a single archive ───────────────

        app.MapGet("/v1/artifacts/{runId}.zip", async (
            string runId,
            HttpContext ctx,
            IArtifactStore store,
            IWorkspaceService wsSvc,
            IProjectService projSvc,
            CancellationToken ct) =>
        {
            var scope = await ScopeFromQueryAsync(ctx, wsSvc, projSvc, ct, runId);
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

            var ms = new MemoryStream();
            var hasFiles = false;
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                await foreach (var entry in store.ListAsync(scope, ct))
                {
                    try
                    {
                        await using var fileStream = await store.ReadAsync(handle, entry.RelativePath, ct);
                        var zipEntry = archive.CreateEntry(entry.RelativePath, CompressionLevel.Fastest);
#pragma warning disable CA1849 // ZipArchiveEntry has no OpenAsync; archive is over MemoryStream
                        await using var entryStream = zipEntry.Open();
#pragma warning restore CA1849
                        await fileStream.CopyToAsync(entryStream, ct).ConfigureAwait(false);
                        hasFiles = true;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Skip unreadable entries — partial zip beats a 500
                    }
                }
            }

            if (!hasFiles)
                return Results.NotFound(new { error = $"No artifacts found for run: {runId}" });

            ms.Position = 0;
            var safeId = SanitizeForFilename(runId);
            ApplyArtifactSecurityHeaders(ctx);
            return Results.File(ms, contentType: "application/zip", fileDownloadName: $"{safeId}.zip");
        });

        // ── Delete a run's artifacts ────────────────────────────────────

        app.MapDelete("/v1/artifacts/{runId}", async (
            string runId,
            HttpContext ctx,
            IArtifactStore store,
            IWorkspaceService wsSvc,
            IProjectService projSvc,
            CancellationToken ct) =>
        {
            var scope = await ScopeFromQueryAsync(ctx, wsSvc, projSvc, ct, runId);
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
    /// Builds an <see cref="ArtifactScope"/> from query-string parameters, resolving
    /// human-readable workspace and project names for composite directory naming.
    /// </summary>
    private static async Task<ArtifactScope> ScopeFromQueryAsync(
        HttpContext ctx,
        IWorkspaceService wsSvc,
        IProjectService projSvc,
        CancellationToken ct,
        string? runId = null)
    {
        var query = ctx.Request.Query;
        var workspaceId = query["workspace_id"].FirstOrDefault()
            ?? ctx.Request.Headers["X-Workspace-Id"].FirstOrDefault()
            ?? WorkspaceIdentity.DefaultPersonal();
        var projectId = query["project_id"].FirstOrDefault()
            ?? ctx.Request.Headers["X-Project-Id"].FirstOrDefault()
            ?? ArtifactScope.DefaultProjectId;

        string? workspaceName = null;
        string? projectName = null;
        try
        {
            var ws = await wsSvc.GetAsync(workspaceId, ct).ConfigureAwait(false);
            workspaceName = ws?.Name;
            if (ws is not null && !string.IsNullOrEmpty(projectId) && projectId != ArtifactScope.DefaultProjectId)
            {
                var proj = await projSvc.GetAsync(projectId, ct).ConfigureAwait(false);
                projectName = proj?.Name;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* best effort — names are cosmetic */ }

        return new ArtifactScope
        {
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            RunId = runId ?? query["run_id"].FirstOrDefault(),
            WorkspaceName = workspaceName,
            ProjectName = projectName,
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

    /// <summary>
    /// Applies security headers that must be present on every artifact response.
    /// HTML/SVG/JS files are served with <c>Content-Disposition: attachment</c> by
    /// <c>Results.Stream</c> (via its fileDownloadName parameter), so this method
    /// only needs to add the non-disposition headers.
    /// </summary>
    private static void ApplyArtifactSecurityHeaders(HttpContext ctx)
    {
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["Cache-Control"] = "private, no-store";
    }

    private static string SanitizeForFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        Span<char> buffer = stackalloc char[Math.Min(name.Length, 64)];
        var len = 0;
        foreach (var c in name)
        {
            if (len >= buffer.Length) break;
            buffer[len++] = Array.IndexOf(invalid, c) >= 0 ? '_' : c;
        }
        return len == 0 ? "artifacts" : new string(buffer[..len]);
    }
}
