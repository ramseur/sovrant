using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Memory;

/// <summary>
/// Handles session-end events by extracting a summary from the JSONL transcript
/// and persisting it via <see cref="IMemoryStore"/>.
/// </summary>
public sealed partial class SessionEndMemoryHandler
{
    private readonly ISessionStore _sessionStore;
    private readonly IMemoryStore _memoryStore;
    private readonly ILogger<SessionEndMemoryHandler> _logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "Session summary extracted for {SessionId}: {TurnCount} turns, {ToolCount} tools, outcome={Outcome}")]
    private static partial void LogSummaryExtracted(ILogger logger, string sessionId, int turnCount, int toolCount, SessionOutcome outcome);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to extract session summary for {SessionId}")]
    private static partial void LogExtractionFailed(ILogger logger, string sessionId, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping summary for session {SessionId}: too few entries ({Count})")]
    private static partial void LogSkippedTooFew(ILogger logger, string sessionId, int count);

    /// <summary>Minimum number of entries a session must have to warrant a summary.</summary>
    private const int MinEntriesForSummary = 4;

    public SessionEndMemoryHandler(
        ISessionStore sessionStore,
        IMemoryStore memoryStore,
        ILogger<SessionEndMemoryHandler> logger)
    {
        _sessionStore = sessionStore;
        _memoryStore = memoryStore;
        _logger = logger;
    }

    /// <summary>
    /// Extracts and saves a session summary. Intended to be called fire-and-forget
    /// when a session is evicted from the pool.
    /// </summary>
    public async Task HandleSessionEndAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
    {
        try
        {
            var entries = await _sessionStore.LoadAsync(sessionId, ct: ct).ConfigureAwait(false);
            if (entries.Count < MinEntriesForSummary)
            {
                LogSkippedTooFew(_logger, sessionId, entries.Count);
                return;
            }

            var project = Directory.GetCurrentDirectory();
            var summary = SessionSummaryExtractor.Extract(sessionId, project, entries, ownerUserId);

            await _memoryStore.SaveSummaryAsync(summary, ct).ConfigureAwait(false);
            LogSummaryExtracted(_logger, sessionId, summary.TurnCount, summary.ToolsUsed.Count, summary.Outcome);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogExtractionFailed(_logger, sessionId, ex);
        }
    }
}
