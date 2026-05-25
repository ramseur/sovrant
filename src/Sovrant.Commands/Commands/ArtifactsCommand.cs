using System.Globalization;
using System.Text;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Phase 53 — CLI command for managing scoped artifacts:
/// <c>/artifacts ls [--workspace ... --project ... --run ...]</c>,
/// <c>/artifacts open &lt;run&gt;</c>,
/// <c>/artifacts rm &lt;run&gt;</c>.
/// </summary>
public sealed class ArtifactsCommand : ISlashCommand
{
    private readonly IArtifactStore _store;

    public ArtifactsCommand(IArtifactStore store) => _store = store;

    public string Name => "artifacts";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "List, open, and remove scoped artifacts.";
    public string Category => "Advanced";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return await ListArtifacts([], ct).ConfigureAwait(false);

        return parts[0] switch
        {
            "ls" or "list" => await ListArtifacts(parts[1..], ct).ConfigureAwait(false),
            "open" => parts.Length > 1 ? await OpenArtifactAsync(parts[1], ct).ConfigureAwait(false) : Err("usage: /artifacts open <run_id>"),
            "rm" or "delete" => parts.Length > 1
                ? await DeleteArtifacts(parts[1], ct).ConfigureAwait(false)
                : Err("usage: /artifacts rm <run_id>"),
            "zip" or "download" => parts.Length > 1
                ? await ZipRunAsync(parts[1], ParseOutputFlag(parts[2..]), ct).ConfigureAwait(false)
                : Err("usage: /artifacts zip <run_id> [--output <path>]"),
            _ => await ListArtifacts(parts, ct).ConfigureAwait(false),
        };
    }

    private async Task<SlashCommandResult> ListArtifacts(string[] flags, CancellationToken ct)
    {
        var scope = ParseScope(flags);

        var entries = new List<ArtifactEntry>();
        await foreach (var entry in _store.ListAsync(scope, ct))
            entries.Add(entry);

        if (entries.Count == 0)
            return new SlashCommandResult("No artifacts found for the given scope.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{"Run",-38} {"Path",-40} {"Size",-10} {"Modified"}");
        sb.AppendLine(new string('-', 100));

        foreach (var e in entries)
        {
            var size = FormatSize(e.SizeBytes);
            var modified = e.LastModified.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{e.RunId ?? "-",-38} {Truncate(e.RelativePath, 40),-40} {size,-10} {modified}");
        }

        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Total: {entries.Count} artifact(s)");

        return new SlashCommandResult(sb.ToString());
    }

    private async Task<SlashCommandResult> OpenArtifactAsync(string runId, CancellationToken ct)
    {
        var scope = new ArtifactScope { RunId = runId };
        var handle = await _store.CreateRunScopeAsync(scope, ct).ConfigureAwait(false);
        var path = handle.Location;

        if (!Directory.Exists(path))
            return Err($"Artifact directory not found: {path}");

        return new SlashCommandResult($"Artifact directory: {path}");
    }

    private async Task<SlashCommandResult> DeleteArtifacts(string runId, CancellationToken ct)
    {
        var scope = new ArtifactScope { RunId = runId };

        await _store.DeleteAsync(scope, ct).ConfigureAwait(false);
        return new SlashCommandResult($"Deleted artifacts for run: {runId}");
    }

    private async Task<SlashCommandResult> ZipRunAsync(string runId, string? outputPath, CancellationToken ct)
    {
        if (_store is not LocalArtifactStore localStore)
            return Err("zip requires a local artifact store.");

        var scope = new ArtifactScope { RunId = runId };

        var count = 0;
        await foreach (var _ in _store.ListAsync(scope, ct))
            count++;

        if (count == 0)
            return new SlashCommandResult($"No artifacts found for run: {runId}");

        // Resolve run directory without creating it, accounting for optional {id}__{name} suffix
        var runDir = FindRunDir(localStore.Root, scope.WorkspaceId, runId);
        if (runDir is null)
            return Err($"Run directory not found for: {runId}");

        var safeId = new string(runId.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
        var destination = outputPath ?? Path.Combine(Environment.CurrentDirectory, $"{safeId}.zip");

        if (File.Exists(destination)) File.Delete(destination);

        await Task.Run(() =>
            System.IO.Compression.ZipFile.CreateFromDirectory(
                runDir, destination,
                System.IO.Compression.CompressionLevel.Fastest,
                includeBaseDirectory: false), ct)
            .ConfigureAwait(false);

        var sizeKb = new FileInfo(destination).Length / 1024.0;
        return new SlashCommandResult(
            $"Created: {destination}\n{count} file(s), {sizeKb:F1} KB");
    }

    private static string? FindRunDir(string root, string workspaceId, string runId)
    {
        // Workspace dir may be bare id or id__name; artifacts always land under {ws}/artifacts/{run}
        var wsDir = FindSegmentDir(root, workspaceId);
        if (wsDir is null) return null;
        var runDir = Path.Combine(wsDir, "artifacts", runId);
        return Directory.Exists(runDir) ? runDir : null;
    }

    private static string? FindSegmentDir(string parent, string id)
    {
        if (!Directory.Exists(parent)) return null;
        var exact = Path.Combine(parent, id);
        if (Directory.Exists(exact)) return exact;
        return Directory.EnumerateDirectories(parent, $"{id}__*").FirstOrDefault();
    }

    private static string? ParseOutputFlag(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == "--output") return args[i + 1];
        return null;
    }

    /// <summary>Parses --workspace, --project, --run flags from the argument list.</summary>
    private static ArtifactScope ParseScope(string[] flags)
    {
        string? workspaceId = null, projectId = null, runId = null;

        for (var i = 0; i < flags.Length; i++)
        {
            if (i + 1 >= flags.Length) continue;

            switch (flags[i])
            {
                case "--workspace":
                    workspaceId = flags[++i];
                    break;
                case "--project":
                    projectId = flags[++i];
                    break;
                case "--run":
                    runId = flags[++i];
                    break;
            }
        }

        return new ArtifactScope
        {
            WorkspaceId = workspaceId ?? WorkspaceIdentity.DefaultPersonal(),
            ProjectId = projectId ?? ArtifactScope.DefaultProjectId,
            RunId = runId,
        };
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB",
    };

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : string.Concat(s.AsSpan(0, maxLen - 3), "...");

    private static SlashCommandResult Err(string msg) =>
        new($"Error: {msg}");
}
