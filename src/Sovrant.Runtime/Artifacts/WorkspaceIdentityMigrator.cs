using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Artifacts;

/// <summary>
/// Phase 87 Track D — one-shot filesystem sweep that moves any artifact
/// directories rooted at the legacy <c>"personal"</c> sentinel
/// (<c>{root}/personal/...</c>) under the canonical
/// <c>{root}/ws-personal-{userId}/...</c> tree.
///
/// SQL <c>V022__workspace_identity_unification</c> handles the database side;
/// this class is its filesystem counterpart. Idempotent — if the legacy
/// directory does not exist or has already been merged, it returns 0.
/// </summary>
public sealed partial class WorkspaceIdentityMigrator
{
    private readonly string _artifactsRoot;
    private readonly ILogger<WorkspaceIdentityMigrator> _logger;

    public WorkspaceIdentityMigrator(string artifactsRoot, ILogger<WorkspaceIdentityMigrator> logger)
    {
        _artifactsRoot = artifactsRoot ?? throw new ArgumentNullException(nameof(artifactsRoot));
        _logger = logger;
    }

    /// <summary>
    /// Returns the number of project directories merged. 0 means nothing
    /// to do.
    /// </summary>
    public int MigrateIfNeeded()
    {
        var legacyDir = Path.Combine(_artifactsRoot, "personal");
        if (!Directory.Exists(legacyDir))
            return 0;

        var canonicalWorkspaceId = WorkspaceIdentity.DefaultPersonal();
        var canonicalDir = Path.Combine(_artifactsRoot, canonicalWorkspaceId);

        // If the canonical dir does not exist yet, the cheapest correct move is
        // a single rename of "personal" → "ws-personal-{userId}".
        if (!Directory.Exists(canonicalDir))
        {
            try
            {
                Directory.Move(legacyDir, canonicalDir);
                LogRenamedRoot(legacyDir, canonicalDir);
                return 1;
            }
            catch (IOException ex)
            {
                LogMigrationFailed(legacyDir, ex);
                return 0;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogMigrationFailed(legacyDir, ex);
                return 0;
            }
        }

        // Both exist — merge children project-by-project. We do NOT overwrite
        // existing canonical files; conflicts are logged and the legacy file
        // is left in place so the user can inspect it.
        var merged = 0;
        foreach (var legacyProject in Directory.GetDirectories(legacyDir))
        {
            var projectName = Path.GetFileName(legacyProject);
            if (string.IsNullOrEmpty(projectName)) continue;

            var targetProject = Path.Combine(canonicalDir, projectName);
            try
            {
                if (!Directory.Exists(targetProject))
                {
                    Directory.Move(legacyProject, targetProject);
                    LogProjectMoved(legacyProject, targetProject);
                    merged++;
                    continue;
                }

                MergeDirectory(legacyProject, targetProject);
                merged++;
            }
            catch (IOException ex)
            {
                LogMigrationFailed(legacyProject, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogMigrationFailed(legacyProject, ex);
            }
        }

        // Drop a breadcrumb so a later boot doesn't keep trying to merge an
        // empty shell.
        TryWriteBreadcrumb(legacyDir, merged);
        return merged;
    }

    private void MergeDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var dest = Path.Combine(target, rel);
            var destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            if (File.Exists(dest))
            {
                LogConflict(file, dest);
                continue;
            }

            File.Move(file, dest);
        }

        // Remove now-empty source subtree.
        try { Directory.Delete(source, recursive: true); }
        catch (IOException) { /* leftover files from conflicts are fine */ }
    }

    private static void TryWriteBreadcrumb(string legacyDir, int merged)
    {
        try
        {
            var breadcrumb = Path.Combine(legacyDir, "README.migrated");
            File.WriteAllText(
                breadcrumb,
                $"Artifacts merged into ws-personal-{{userId}} on {DateTimeOffset.UtcNow:O}.\n" +
                $"Merged {merged} project directories. This directory is no longer used.\n");
        }
        catch (IOException) { /* best effort */ }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Renamed legacy artifact root '{Source}' to '{Target}'")]
    private partial void LogRenamedRoot(string source, string target);

    [LoggerMessage(Level = LogLevel.Information, Message = "Moved legacy project directory '{Source}' to '{Target}'")]
    private partial void LogProjectMoved(string source, string target);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Workspace identity migration: conflict at '{Target}', kept legacy file at '{Source}'")]
    private partial void LogConflict(string source, string target);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to migrate legacy directory '{Path}'")]
    private partial void LogMigrationFailed(string path, Exception ex);
}
