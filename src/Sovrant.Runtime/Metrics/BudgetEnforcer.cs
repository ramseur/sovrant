using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Metrics;

/// <summary>
/// Enforces per-session and per-project cost budgets.
/// Warns at 80% spend, blocks new turns at 100%.
/// Budgets are configured via env vars: <c>SOVRANT_SESSION_BUDGET_USD</c> and <c>SOVRANT_PROJECT_BUDGET_USD</c>.
/// </summary>
public sealed partial class BudgetEnforcer
{
    private readonly decimal? _sessionBudgetUsd;
    private readonly decimal? _projectBudgetUsd;
    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<string, decimal> _sessionSpend = new(StringComparer.OrdinalIgnoreCase);
    private decimal _projectSpend;
    private readonly object _projectLock = new();

    private readonly ConcurrentDictionary<string, bool> _sessionWarned80 = new(StringComparer.OrdinalIgnoreCase);
    private bool _projectWarned80;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session {SessionId} has used {Percent:F0}% of its ${Budget:F2} budget (${Spent:F4} spent)")]
    private static partial void LogSessionBudgetWarning(ILogger logger, string sessionId, double percent, decimal budget, decimal spent);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Project budget: {Percent:F0}% of ${Budget:F2} used (${Spent:F4} spent)")]
    private static partial void LogProjectBudgetWarning(ILogger logger, double percent, decimal budget, decimal spent);

    [LoggerMessage(Level = LogLevel.Error, Message = "Session {SessionId} has exceeded its ${Budget:F2} budget (${Spent:F4} spent) — blocking")]
    private static partial void LogSessionBudgetExceeded(ILogger logger, string sessionId, decimal budget, decimal spent);

    [LoggerMessage(Level = LogLevel.Error, Message = "Project budget of ${Budget:F2} exceeded (${Spent:F4} spent) — blocking")]
    private static partial void LogProjectBudgetExceeded(ILogger logger, decimal budget, decimal spent);

    public BudgetEnforcer(ILogger<BudgetEnforcer> logger, decimal? sessionBudgetUsd = null, decimal? projectBudgetUsd = null)
    {
        _logger = logger;

        // Env vars override constructor params
        _sessionBudgetUsd = TryParseEnv("SOVRANT_SESSION_BUDGET_USD") ?? sessionBudgetUsd;
        _projectBudgetUsd = TryParseEnv("SOVRANT_PROJECT_BUDGET_USD") ?? projectBudgetUsd;
    }

    /// <summary>Whether any budget cap is configured.</summary>
    public bool IsEnabled => _sessionBudgetUsd.HasValue || _projectBudgetUsd.HasValue;

    /// <summary>Configured session budget, or <see langword="null"/> if uncapped.</summary>
    public decimal? SessionBudgetUsd => _sessionBudgetUsd;

    /// <summary>Configured project budget, or <see langword="null"/> if uncapped.</summary>
    public decimal? ProjectBudgetUsd => _projectBudgetUsd;

    /// <summary>
    /// Records a cost and checks budget thresholds.
    /// Returns a <see cref="BudgetStatus"/> indicating whether the turn should proceed.
    /// </summary>
    public BudgetStatus RecordSpend(string sessionId, decimal costUsd)
    {
        // Track session spend
        var sessionTotal = _sessionSpend.AddOrUpdate(sessionId, costUsd, (_, prev) => prev + costUsd);

        // Track project spend
        decimal projectTotal;
        lock (_projectLock)
        {
            _projectSpend += costUsd;
            projectTotal = _projectSpend;
        }

        // Check session budget
        if (_sessionBudgetUsd.HasValue)
        {
            var pct = (double)(sessionTotal / _sessionBudgetUsd.Value) * 100;

            if (sessionTotal >= _sessionBudgetUsd.Value)
            {
                LogSessionBudgetExceeded(_logger, sessionId, _sessionBudgetUsd.Value, sessionTotal);
                return BudgetStatus.Exceeded;
            }

            if (pct >= 80 && _sessionWarned80.TryAdd(sessionId, true))
                LogSessionBudgetWarning(_logger, sessionId, pct, _sessionBudgetUsd.Value, sessionTotal);
        }

        // Check project budget
        if (_projectBudgetUsd.HasValue)
        {
            var pct = (double)(projectTotal / _projectBudgetUsd.Value) * 100;

            if (projectTotal >= _projectBudgetUsd.Value)
            {
                LogProjectBudgetExceeded(_logger, _projectBudgetUsd.Value, projectTotal);
                return BudgetStatus.Exceeded;
            }

            if (pct >= 80 && !_projectWarned80)
            {
                _projectWarned80 = true;
                LogProjectBudgetWarning(_logger, pct, _projectBudgetUsd.Value, projectTotal);
            }
        }

        return BudgetStatus.Ok;
    }

    /// <summary>Gets the total spend for a session.</summary>
    public decimal GetSessionSpend(string sessionId) =>
        _sessionSpend.TryGetValue(sessionId, out var total) ? total : 0;

    /// <summary>Gets the total project spend across all sessions.</summary>
    public decimal ProjectSpend
    {
        get { lock (_projectLock) return _projectSpend; }
    }

    /// <summary>
    /// Checks whether a new turn should be allowed for the given session,
    /// without recording any spend.
    /// </summary>
    public BudgetStatus Check(string sessionId)
    {
        if (_sessionBudgetUsd.HasValue)
        {
            var sessionTotal = _sessionSpend.TryGetValue(sessionId, out var s) ? s : 0;
            if (sessionTotal >= _sessionBudgetUsd.Value)
                return BudgetStatus.Exceeded;
        }

        if (_projectBudgetUsd.HasValue)
        {
            var projectTotal = ProjectSpend;
            if (projectTotal >= _projectBudgetUsd.Value)
                return BudgetStatus.Exceeded;
        }

        return BudgetStatus.Ok;
    }

    private static decimal? TryParseEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value is not null && decimal.TryParse(value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result : null;
    }
}

/// <summary>Result of a budget check.</summary>
public enum BudgetStatus
{
    /// <summary>Spend is within budget.</summary>
    Ok,

    /// <summary>Budget has been exceeded — block the turn.</summary>
    Exceeded
}
