using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

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

    private readonly IMemoryStore _store;
    private readonly ILogger<MemoryInjector> _logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Injecting memory: {Summaries} summaries, {Patterns} patterns, {Instincts} instincts for project '{Project}'")]
    private static partial void LogInjection(ILogger logger, int summaries, int patterns, int instincts, string project);

    public MemoryInjector(IMemoryStore store, ILogger<MemoryInjector> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Builds a memory section to append to the system prompt.
    /// Returns an empty string if no relevant memories are found.
    /// </summary>
    /// <param name="project">The current project directory.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<string> BuildMemorySectionAsync(string project, CancellationToken ct = default)
    {
        var summariesTask = _store.LoadSummariesAsync(project, MaxSummaries, ct);
        var patternsTask = _store.LoadPatternsAsync(project, ct);
        var instinctsTask = _store.LoadInstinctsAsync(MinInstinctConfidence, ct);

        await Task.WhenAll(summariesTask, patternsTask, instinctsTask).ConfigureAwait(false);

        var summaries = await summariesTask.ConfigureAwait(false);
        var patterns = await patternsTask.ConfigureAwait(false);
        var instincts = await instinctsTask.ConfigureAwait(false);

        if (summaries.Count == 0 && patterns.Count == 0 && instincts.Count == 0)
            return string.Empty;

        LogInjection(_logger, summaries.Count, patterns.Count, instincts.Count, project);

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

        return sb.ToString();
    }
}
