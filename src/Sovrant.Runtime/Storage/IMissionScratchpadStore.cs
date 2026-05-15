namespace Sovrant.Runtime.Storage;

/// <summary>
/// Phase 51 — typed, append-only shared store used by parallel sub-agents
/// within one mission to publish intermediate findings the next plan wave
/// can read. Keyed by mission + step + namespace + key, so a mission's
/// sub-agents can see each other's outputs without routing through the
/// orchestrator as an integration point.
///
/// Rows are append-only: writing the same (mission, namespace, key) twice
/// does NOT overwrite — both rows are preserved and readers choose between
/// "all values" (history) and "latest value" (effective state). This
/// matches the way the executor treats every plan-level event as a trace
/// row rather than a mutable piece of state.
///
/// Values are opaque JSON; shape is owned by the mission planner /
/// sub-agent that wrote them. The store never inspects the payload.
/// </summary>
public interface IMissionScratchpadStore
{
    /// <summary>
    /// Appends one scratchpad entry. The underlying table auto-assigns an
    /// id and a <c>created_at</c> timestamp; callers don't need to sequence
    /// writes across sub-agents.
    /// </summary>
    Task AppendAsync(MissionScratchpadEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Loads every entry for a mission, optionally filtered to one step
    /// and/or one namespace. Returned in insertion order (oldest first)
    /// so a reader can reason about what a sub-agent saw at each moment.
    /// </summary>
    Task<IReadOnlyList<MissionScratchpadEntry>> LoadAsync(
        string missionId,
        int? stepIndex = null,
        string? @namespace = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent value for one (mission, namespace, key)
    /// tuple, or <c>null</c> if the key has never been written. Used by
    /// the "effective state" readers — e.g. "what's the current list of
    /// suspect files this mission's sub-agents agreed on?"
    /// </summary>
    Task<MissionScratchpadEntry?> ReadLatestAsync(
        string missionId,
        string @namespace,
        string key,
        CancellationToken ct = default);

    /// <summary>Deletes every scratchpad entry for a mission. Returns rows deleted.</summary>
    Task<int> DeleteMissionAsync(string missionId, CancellationToken ct = default);
}

/// <summary>
/// One row in <c>mission_scratchpad</c>. The store auto-populates
/// <see cref="CreatedAt"/> on write, so callers may leave it at default
/// when appending — the value you read back is whatever SQLite stamped.
/// </summary>
/// <param name="MissionId">Mission family id the entry belongs to.</param>
/// <param name="StepIndex">Plan step index during which the entry was written. Lets readers filter to "what did the sub-agents publish during step 3?".</param>
/// <param name="Namespace">Free-form bucket name — e.g. "findings", "hypotheses", "blockers". Defaults to <c>"default"</c> in the SQL layer when omitted.</param>
/// <param name="Key">Logical key inside the namespace. Append-only: writing the same key twice produces two rows.</param>
/// <param name="Value">Opaque JSON payload. Store never inspects it.</param>
/// <param name="AgentId">Sub-agent that wrote the entry, or <c>null</c> if the executor wrote it directly.</param>
/// <param name="CreatedAt">Wall-clock time when the row was inserted. Ignored on write (SQLite stamps it); populated on read.</param>
/// <param name="WorkspaceId">Workspace the mission belongs to, for per-workspace cleanup.</param>
/// <param name="ProjectId">Project the mission belongs to, if any.</param>
public sealed record MissionScratchpadEntry(
    string MissionId,
    int StepIndex,
    string Namespace,
    string Key,
    string Value,
    string? AgentId = null,
    DateTimeOffset CreatedAt = default,
    string? WorkspaceId = null,
    string? ProjectId = null);
