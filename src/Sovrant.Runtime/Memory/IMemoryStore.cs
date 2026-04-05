namespace Sovrant.Runtime.Memory;

/// <summary>
/// Unified interface for the three memory layers: session summaries, learned patterns, and instincts.
/// Each layer has different persistence, scope, and retrieval characteristics.
/// </summary>
public interface IMemoryStore
{
    // ── Session Summaries ───────────────────────────────────────────────

    /// <summary>Persists a session summary.</summary>
    Task SaveSummaryAsync(SessionSummary summary, CancellationToken ct = default);

    /// <summary>Loads the most recent session summaries for a project (up to <paramref name="maxCount"/>).</summary>
    Task<IReadOnlyList<SessionSummary>> LoadSummariesAsync(string project, int maxCount = 5, CancellationToken ct = default);

    // ── Learned Patterns ────────────────────────────────────────────────

    /// <summary>Adds or updates a learned pattern for a project.</summary>
    Task SavePatternAsync(LearnedPattern pattern, CancellationToken ct = default);

    /// <summary>Loads all learned patterns for a project, ordered by confidence descending.</summary>
    Task<IReadOnlyList<LearnedPattern>> LoadPatternsAsync(string project, CancellationToken ct = default);

    /// <summary>Removes a learned pattern by ID.</summary>
    Task RemovePatternAsync(string id, string project, CancellationToken ct = default);

    // ── Instincts ───────────────────────────────────────────────────────

    /// <summary>Adds or updates an instinct.</summary>
    Task SaveInstinctAsync(Instinct instinct, CancellationToken ct = default);

    /// <summary>Loads all instincts above the prune threshold, ordered by confidence descending.</summary>
    Task<IReadOnlyList<Instinct>> LoadInstinctsAsync(double minConfidence = Instinct.PruneThreshold, CancellationToken ct = default);

    /// <summary>Reinforces an instinct (increases confidence) and appends evidence.</summary>
    Task ReinforceInstinctAsync(string id, string evidence, CancellationToken ct = default);

    /// <summary>Corrects an instinct (decreases confidence) and appends evidence. Prunes if below threshold.</summary>
    Task CorrectInstinctAsync(string id, string evidence, CancellationToken ct = default);

    /// <summary>Removes an instinct by ID.</summary>
    Task RemoveInstinctAsync(string id, CancellationToken ct = default);

    /// <summary>Prunes all instincts below the threshold. Returns the number pruned.</summary>
    Task<int> PruneInstinctsAsync(double threshold = Instinct.PruneThreshold, CancellationToken ct = default);
}
