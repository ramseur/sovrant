namespace Sovrant.Runtime.Memory;

/// <summary>
/// Unified interface for the three memory layers: session summaries, learned patterns, and instincts.
/// Each layer has different persistence, scope, and retrieval characteristics.
/// All methods accept an optional <c>workspaceId</c> parameter for workspace-scoped queries (Phase 35).
/// </summary>
public interface IMemoryStore
{
    // ── Session Summaries ───────────────────────────────────────────────

    /// <summary>Persists a session summary.</summary>
    Task SaveSummaryAsync(SessionSummary summary, CancellationToken ct = default);

    /// <summary>Loads the most recent session summaries for a project. When <paramref name="ownerUserId"/> is set, returns only that user's summaries plus unowned legacy rows.</summary>
    Task<IReadOnlyList<SessionSummary>> LoadSummariesAsync(string project, int maxCount = 5, string? ownerUserId = null, CancellationToken ct = default);

    // ── Learned Patterns ────────────────────────────────────────────────

    /// <summary>Adds or updates a learned pattern for a project.</summary>
    Task SavePatternAsync(LearnedPattern pattern, CancellationToken ct = default);

    /// <summary>Loads all learned patterns for a project. When <paramref name="ownerUserId"/> is set, returns only that user's patterns plus unowned legacy rows.</summary>
    Task<IReadOnlyList<LearnedPattern>> LoadPatternsAsync(string project, string? ownerUserId = null, CancellationToken ct = default);

    /// <summary>Removes a learned pattern by ID.</summary>
    Task RemovePatternAsync(string id, string project, CancellationToken ct = default);

    // ── Instincts ───────────────────────────────────────────────────────

    /// <summary>Adds or updates an instinct.</summary>
    Task SaveInstinctAsync(Instinct instinct, CancellationToken ct = default);

    /// <summary>Loads all instincts above the prune threshold. When <paramref name="ownerUserId"/> is set, returns only that user's instincts plus unowned legacy rows.</summary>
    Task<IReadOnlyList<Instinct>> LoadInstinctsAsync(double minConfidence = Instinct.PruneThreshold, string? ownerUserId = null, CancellationToken ct = default);

    /// <summary>Reinforces an instinct (increases confidence) and appends evidence.</summary>
    Task ReinforceInstinctAsync(string id, string evidence, CancellationToken ct = default);

    /// <summary>Corrects an instinct (decreases confidence) and appends evidence. Prunes if below threshold.</summary>
    Task CorrectInstinctAsync(string id, string evidence, CancellationToken ct = default);

    /// <summary>Removes an instinct by ID.</summary>
    Task RemoveInstinctAsync(string id, CancellationToken ct = default);

    /// <summary>Prunes all instincts below the threshold. Returns the number pruned.</summary>
    Task<int> PruneInstinctsAsync(double threshold = Instinct.PruneThreshold, CancellationToken ct = default);
}
