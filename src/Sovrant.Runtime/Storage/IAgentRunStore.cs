namespace Sovrant.Runtime.Storage;

/// <summary>
/// Phase 52 — unified run ledger that tracks every agentic execution
/// (delegation, swarm-task, mission-step) in the <c>agent_runs</c> table.
/// </summary>
public interface IAgentRunStore
{
    Task<AgentRunRecord> CreateAsync(AgentRunRecord run, CancellationToken ct = default);
    Task<AgentRunRecord?> GetAsync(string runId, CancellationToken ct = default);
    Task UpdateStatusAsync(string runId, string status, int inputTokens = 0, int outputTokens = 0, decimal? costUsd = null, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRunRecord>> ListAsync(AgentRunFilter? filter = null, int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// Phase 99 — flips the privacy flag on an agent run. The owner predicate
    /// is enforced inside the UPDATE so a mismatched owner is a silent
    /// no-op (zero rows affected) and never leaks the row's existence.
    /// </summary>
    Task UpdatePrivacyAsync(string runId, string ownerUserId, bool isPrivate, CancellationToken ct = default);
}

/// <summary>Represents a row in the <c>agent_runs</c> table.</summary>
public sealed record AgentRunRecord(
    string RunId,
    string? ParentRunId,
    string? TeamId,
    string? MemberId,
    string WorkspaceId,
    string? ProjectId,
    string UserId,
    string Kind,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt = null,
    int InputTokens = 0,
    int OutputTokens = 0,
    decimal? CostUsd = null,
    string? Prompt = null,
    bool IsPrivate = true);

/// <summary>Filter for <see cref="IAgentRunStore.ListAsync"/>.</summary>
public sealed record AgentRunFilter(
    string? WorkspaceId = null,
    string? ProjectId = null,
    string? UserId = null,
    string? TeamId = null,
    string? MemberId = null,
    string? Kind = null,
    string? Status = null,
    string? ParentRunId = null);
