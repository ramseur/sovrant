namespace Sovrant.Runtime.Evals;

/// <summary>
/// Abstraction for persisting and querying eval reports.
/// </summary>
public interface IEvalResultStore
{
    /// <summary>Saves an eval report.</summary>
    Task SaveAsync(EvalReport report, CancellationToken ct = default);

    /// <summary>Loads historical report summaries for a given suite (most recent first).</summary>
    IReadOnlyList<EvalReportSummary> LoadHistory(string suiteName, int maxResults = 50);
}
