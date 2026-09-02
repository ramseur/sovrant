using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Artifacts;

/// <summary>
/// One-shot migrator that moves the old flat artifact layout
/// (<c>~/.sovrant/artifacts/{ws}/{proj}/{run}/</c>) into the new
/// workspace-first layout (<c>~/.sovrant/workspaces/{ws}/...</c>).
///
/// Projects-only routing: every project segment — including the old
/// <c>default-project</c> sentinel — lands under <c>{ws}/projects/{proj}/artifacts/{run}/</c>.
/// There is no workspace-level (project-less) destination.
///
/// Idempotent — if the old root does not exist or has already been migrated,
/// returns 0 immediately. Leaves a <c>README.migrated</c> breadcrumb in the
/// old root so subsequent boots skip it.
/// </summary>
public sealed partial class ArtifactLayoutMigrator
{
    private readonly string _oldRoot;   // ~/.sovrant/artifacts
    private readonly string _newRoot;   // ~/.sovrant/workspaces
    private readonly ILogger<ArtifactLayoutMigrator> _logger;

    public ArtifactLayoutMigrator(string newRoot, ILogger<ArtifactLayoutMigrator> logger)
    {
        _newRoot = newRoot ?? throw new ArgumentNullException(nameof(newRoot));
        // Old root is always the sibling "artifacts" directory next to the new "workspaces" root.
        _oldRoot = Path.Combine(Path.GetDirectoryName(newRoot)!, "artifacts");
        _logger = logger;
    }

    /// <summary>Returns the number of run directories migrated, or 0 if nothing to do.</summary>
    public int MigrateIfNeeded()
    {
        if (!Directory.Exists(_oldRoot))
            return 0;

        // Breadcrumb means we already ran.
        if (File.Exists(Path.Combine(_oldRoot, "README.migrated")))
            return 0;

        var workspaceDirs = Directory.GetDirectories(_oldRoot);
        if (workspaceDirs.Length == 0)
            return 0;

        LogMigrationStart(_oldRoot, _newRoot);

        var totalRuns = 0;
        foreach (var wsDir in workspaceDirs)
        {
            var wsName = Path.GetFileName(wsDir);
            if (string.IsNullOrEmpty(wsName)) continue;

            foreach (var projDir in Directory.GetDirectories(wsDir))
            {
                var projName = Path.GetFileName(projDir);
                if (string.IsNullOrEmpty(projName)) continue;

                foreach (var runDir in Directory.GetDirectories(projDir))
                {
                    var runName = Path.GetFileName(runDir);
                    if (string.IsNullOrEmpty(runName)) continue;

                    var targetDir = BuildNewPath(wsName, projName, runName);
                    MoveRunDirectory(runDir, targetDir);
                    totalRuns++;
                }

                // Move any loose files in the project dir (shouldn't exist, but be safe)
                foreach (var file in Directory.GetFiles(projDir))
                    TryMoveLooseFile(file, wsName, projName);
            }
        }

        TryWriteBreadcrumb(totalRuns);
        LogMigrationComplete(totalRuns);
        return totalRuns;
    }

    private string BuildNewPath(string wsSegment, string projSegment, string runSegment)
    {
        // Projects-only: every project segment, including the legacy "default-project"
        // sentinel, nests under projects/{proj}/artifacts — no workspace-level bypass.
        var wsDir = Path.Combine(_newRoot, wsSegment);
        var artifactsDir = Path.Combine(wsDir, "projects", projSegment, "artifacts");

        return Path.Combine(artifactsDir, runSegment);
    }

    private void MoveRunDirectory(string source, string target)
    {
        try
        {
            if (Directory.Exists(target))
            {
                MergeDirectory(source, target);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Directory.Move(source, target);
            LogRunMoved(source, target);
        }
        catch (IOException ex) { LogMoveFailed(source, ex); }
        catch (UnauthorizedAccessException ex) { LogMoveFailed(source, ex); }
    }

    private void MergeDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var dest = Path.Combine(target, rel);
            var destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

            if (File.Exists(dest)) { LogConflict(file, dest); continue; }
            File.Move(file, dest);
        }
        try { Directory.Delete(source, recursive: true); }
        catch (IOException) { /* leftover conflict files */ }
    }

    private void TryMoveLooseFile(string file, string wsSegment, string projSegment)
    {
        try
        {
            var fileName = Path.GetFileName(file);
            var targetDir = Path.Combine(_newRoot, wsSegment, "projects", projSegment, "artifacts");
            Directory.CreateDirectory(targetDir);
            var dest = Path.Combine(targetDir, fileName);
            if (!File.Exists(dest)) File.Move(file, dest);
        }
        catch (IOException ex) { LogMoveFailed(file, ex); }
    }

    private void TryWriteBreadcrumb(int totalRuns)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(_oldRoot, "README.migrated"),
                $"Migrated to workspace-first layout on {DateTimeOffset.UtcNow:O}.\n" +
                $"Moved {totalRuns} run directories to {_newRoot}.\n" +
                "This directory is no longer used by Sovrant.\n");
        }
        catch (IOException) { /* best effort */ }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Migrating artifact layout from '{OldRoot}' to '{NewRoot}'")]
    private partial void LogMigrationStart(string oldRoot, string newRoot);

    [LoggerMessage(Level = LogLevel.Information, Message = "Moved run directory '{Source}' → '{Target}'")]
    private partial void LogRunMoved(string source, string target);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conflict: kept existing file at '{Target}', skipped '{Source}'")]
    private partial void LogConflict(string source, string target);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to move '{Path}'")]
    private partial void LogMoveFailed(string path, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Artifact layout migration complete: {TotalRuns} run directories moved")]
    private partial void LogMigrationComplete(int totalRuns);
}
