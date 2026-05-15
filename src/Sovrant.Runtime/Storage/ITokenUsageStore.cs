namespace Sovrant.Runtime.Storage;

/// <summary>
/// Persists token usage records per turn for billing/analytics.
/// </summary>
public interface ITokenUsageStore
{
    /// <summary>Records a token usage entry.</summary>
    Task RecordAsync(TokenUsageRecord record, CancellationToken ct = default);

    /// <summary>Gets total token usage for a session.</summary>
    Task<TokenUsageSummary> GetSessionTotalsAsync(string sessionId, CancellationToken ct = default);
}

/// <summary>A single token usage entry.</summary>
public sealed record TokenUsageRecord(
    string SessionId,
    string Model,
    int InputTokens,
    int OutputTokens,
    decimal? CostUsd = null);

/// <summary>Aggregated token usage for a session.</summary>
public sealed record TokenUsageSummary(
    int TotalInputTokens,
    int TotalOutputTokens,
    decimal? TotalCostUsd);
