using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Artifacts;

/// <summary>
/// One-shot importer that migrates artifacts from the legacy flat
/// <c>{cwd}/artifacts/</c> layout into the scoped
/// <c>{root}/default-user/personal/default-project/legacy-{slug}/</c> layout.
/// Runs at startup if the legacy directory is non-empty and
/// <c>SOVRANT_ARTIFACTS_MIGRATE_LEGACY</c> is not <c>false</c>.
/// </summary>
public sealed partial class LegacyArtifactImporter
{
    private readonly IArtifactStore _store;
    private readonly ILogger<LegacyArtifactImporter> _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
    };

    public LegacyArtifactImporter(IArtifactStore store, ILogger<LegacyArtifactImporter> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Runs the legacy import if applicable. Returns the number of
    /// directories migrated, or 0 if nothing was done.
    /// </summary>
    public async Task<int> ImportIfNeededAsync(CancellationToken ct = default)
    {
        // Check opt-out
        var migrate = Environment.GetEnvironmentVariable("SOVRANT_ARTIFACTS_MIGRATE_LEGACY");
        if (string.Equals(migrate, "false", StringComparison.OrdinalIgnoreCase))
            return 0;

        // Only migrate if the override root is not set (i.e. user hasn't
        // already configured a custom location)
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SOVRANT_ARTIFACTS_ROOT")))
            return 0;

        var legacyRoot = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
        if (!Directory.Exists(legacyRoot))
            return 0;

        var slugDirs = Directory.GetDirectories(legacyRoot);
        if (slugDirs.Length == 0)
            return 0;

        LogMigrationStart(slugDirs.Length);

        var migrated = 0;
        foreach (var slugDir in slugDirs)
        {
            ct.ThrowIfCancellationRequested();

            var slug = Path.GetFileName(slugDir);
            if (string.IsNullOrEmpty(slug)) continue;

            var runId = $"legacy-{slug}";
            var scope = new ArtifactScope
            {
                WorkspaceId = WorkspaceIdentity.DefaultPersonal(),
                ProjectId = ArtifactScope.DefaultProjectId,
                RunId = runId,
            };

            try
            {
                var handle = await _store.CreateRunScopeAsync(scope, ct).ConfigureAwait(false);

                // Copy all files
                foreach (var file in Directory.EnumerateFiles(slugDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(slugDir, file).Replace('\\', '/');
                    using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await _store.WriteAsync(handle, relativePath, fs, ct: ct).ConfigureAwait(false);
                }

                // Write migration manifest
                var manifest = new ArtifactManifest
                {
                    RunId = runId,
                    WorkspaceId = WorkspaceIdentity.DefaultPersonal(),
                    ProjectId = ArtifactScope.DefaultProjectId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    MigratedFrom = "legacy",
                    OriginalSlug = slug,
                };

                foreach (var f in Directory.EnumerateFiles(slugDir, "*", SearchOption.AllDirectories))
                    manifest.Files.Add(Path.GetRelativePath(slugDir, f).Replace('\\', '/'));

                var manifestJson = JsonSerializer.Serialize(manifest, s_jsonOptions);
                using var manifestStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(manifestJson));
                await _store.WriteAsync(handle, "_manifest.json", manifestStream, "application/json", ct).ConfigureAwait(false);

                migrated++;
                LogSlugMigrated(slug, runId);
            }
            catch (IOException ex)
            {
                LogMigrationFailed(slug, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogMigrationFailed(slug, ex);
            }
        }

        // Leave a breadcrumb in the old directory
        if (migrated > 0)
        {
            try
            {
                var breadcrumb = Path.Combine(legacyRoot, "README.migrated");
                await File.WriteAllTextAsync(
                    breadcrumb,
                    $"Artifacts migrated to scoped layout on {DateTimeOffset.UtcNow:O}.\n" +
                    $"Migrated {migrated} directories.\n" +
                    "This directory is no longer used by Sovrant.\n",
                    ct).ConfigureAwait(false);
            }
            catch (IOException) { /* best effort */ }
        }

        LogMigrationComplete(migrated);
        return migrated;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Migrating {Count} legacy artifact directories to scoped layout")]
    private partial void LogMigrationStart(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrated legacy artifact '{Slug}' → '{RunId}'")]
    private partial void LogSlugMigrated(string slug, string runId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to migrate legacy artifact '{Slug}'")]
    private partial void LogMigrationFailed(string slug, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Legacy artifact migration complete: {Count} directories migrated")]
    private partial void LogMigrationComplete(int count);
}
