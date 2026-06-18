using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Memory;

/// <summary>
/// Selects relevant memories from all three layers and formats them for injection
/// into the system prompt. Limits total injected content to avoid bloating context.
/// </summary>
public sealed partial class MemoryInjector
{
    /// <summary>Maximum number of session summaries to inject.</summary>
    private const int MaxSummaries = 3;

    /// <summary>Maximum number of learned patterns to inject.</summary>
    private const int MaxPatterns = 15;

    /// <summary>Maximum number of instincts to inject.</summary>
    private const int MaxInstincts = 10;

    /// <summary>Minimum instinct confidence to include in the prompt.</summary>
    private const double MinInstinctConfidence = 0.4;

    /// <summary>Maximum number of user-saved workspace_memory entries to inject (Phase 81).</summary>
    private const int MaxWorkspaceEntries = 10;

    private readonly IMemoryStore _store;
    private readonly IWorkspaceService? _workspaceService;
    private readonly ILogger<MemoryInjector> _logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Injecting memory: {Summaries} summaries, {Patterns} patterns, {Instincts} instincts, {Workspace} workspace entries for project '{Project}'")]
    private static partial void LogInjection(ILogger logger, int summaries, int patterns, int instincts, int workspace, string project);

    public MemoryInjector(IMemoryStore store, ILogger<MemoryInjector> logger, IWorkspaceService? workspaceService = null)
    {
        _store = store;
        _logger = logger;
        _workspaceService = workspaceService;
    }

    /// <summary>
    /// Builds a memory section to append to the system prompt.
    /// Returns an empty string if no relevant memories are found.
    /// </summary>
    /// <param name="project">The current project directory.</param>
    /// <param name="workspaceId">Workspace ID for loading user-saved <c>workspace_memory</c> rows. If null, that section is skipped.</param>
    /// <param name="projectId">Project ID for filtering project-scoped workspace memory. Workspace-wide entries (NULL project_id) always included.</param>
    /// <param name="ownerUserId">The session owner. Used to filter workspace memory so each user only sees public entries plus their own private ones.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<string> BuildMemorySectionAsync(string project, string? workspaceId = null, string? projectId = null, string? ownerUserId = null, CancellationToken ct = default)
    {
        var summariesTask = _store.LoadSummariesAsync(project, MaxSummaries, ownerUserId: ownerUserId, ct: ct);
        var patternsTask = _store.LoadPatternsAsync(project, ownerUserId: ownerUserId, ct: ct);
        var instinctsTask = _store.LoadInstinctsAsync(MinInstinctConfidence, ownerUserId: ownerUserId, ct: ct);
        var workspaceTask = (_workspaceService is not null && !string.IsNullOrEmpty(workspaceId))
            ? _workspaceService.ListMemoryAsync(workspaceId, layer: null, viewerUserId: ownerUserId, ct)
            : Task.FromResult<IReadOnlyList<WorkspaceMemoryEntry>>(Array.Empty<WorkspaceMemoryEntry>());

        await Task.WhenAll(summariesTask, patternsTask, instinctsTask, workspaceTask).ConfigureAwait(false);

        var summaries = await summariesTask.ConfigureAwait(false);
        var patterns = await patternsTask.ConfigureAwait(false);
        var instincts = await instinctsTask.ConfigureAwait(false);
        var workspaceAll = await workspaceTask.ConfigureAwait(false);

        // Keep workspace-wide entries (project_id IS NULL) plus those scoped to the active project.
        var workspace = workspaceAll
            .Where(e => e.ProjectId is null || e.ProjectId == projectId)
            .ToList();

        if (summaries.Count == 0 && patterns.Count == 0 && instincts.Count == 0 && workspace.Count == 0)
            return string.Empty;

        LogInjection(_logger, summaries.Count, patterns.Count, instincts.Count, workspace.Count, project);

        var sb = new StringBuilder();

        // ── Session Summaries (recent context) ─────────────────────
        if (summaries.Count > 0)
        {
            sb.Append("\n\n---\n## Recent sessions\n\n");
            foreach (var s in summaries.Take(MaxSummaries))
            {
                sb.Append("- **").Append(s.EndedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append("** — ");
                if (s.Tasks.Count > 0)
                    sb.Append(s.Tasks[0]);
                else
                    sb.Append("(no task recorded)");

                sb.Append(" | Tools: ").Append(string.Join(", ", s.ToolsUsed.Take(5)));
                if (s.FilesModified.Count > 0)
                    sb.Append(" | Files: ").Append(string.Join(", ", s.FilesModified.Take(3)));
                sb.Append(" | ").Append(s.Outcome);
                sb.AppendLine();
            }
        }

        // ── Learned Patterns (project conventions) ──────────────────
        if (patterns.Count > 0)
        {
            sb.Append("\n---\n## Learned patterns for this project\n\n");
            foreach (var p in patterns.Take(MaxPatterns))
            {
                sb.Append("- ").Append(p.Pattern);
                if (p.Confidence < 0.7)
                    sb.Append(CultureInfo.InvariantCulture, $" (confidence: {p.Confidence:F1})");
                sb.AppendLine();
            }
        }

        // ── Instincts (behavioral rules) ────────────────────────────
        if (instincts.Count > 0)
        {
            sb.Append("\n---\n## Behavioral instincts\n\n");
            foreach (var i in instincts.Take(MaxInstincts))
            {
                sb.Append("- When **").Append(i.Trigger).Append("** → ").Append(i.Action);
                sb.Append(CultureInfo.InvariantCulture, $" (confidence: {i.Confidence:F2})");
                sb.AppendLine();
            }
        }

        // ── Workspace memory (user-saved via UI/API, Phase 81) ──────
        if (workspace.Count > 0)
        {
            sb.Append("\n---\n## Workspace memory (user-saved)\n\n");
            foreach (var e in workspace.Take(MaxWorkspaceEntries))
            {
                sb.Append("- [").Append(e.Layer).Append("] ").Append(e.Content);
                if (e.Confidence is { } c && c < 0.7)
                    sb.Append(CultureInfo.InvariantCulture, $" (confidence: {c:F2})");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
