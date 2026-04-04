using Sovrant.Runtime.Conversation;

namespace Sovrant.Server;

/// <summary>
/// Background service that periodically evicts idle sessions from the <see cref="IRuntimeSessionPool"/>
/// based on TTL and max-sessions cap. Runs every 5 minutes.
/// </summary>
internal sealed partial class SessionEvictionService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private readonly IRuntimeSessionPool _pool;
    private readonly ILogger<SessionEvictionService> _logger;
    private readonly TimeSpan _ttl;
    private readonly int _maxSessions;

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Session eviction sweep: evicted {Count} sessions ({ActiveBefore} → {ActiveAfter} active)")]
    private static partial void LogEvictionSweep(ILogger logger, int count, int activeBefore, int activeAfter);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Session eviction sweep: no sessions evicted ({Active} active)")]
    private static partial void LogNoEviction(ILogger logger, int active);

    public SessionEvictionService(
        IRuntimeSessionPool pool,
        ILogger<SessionEvictionService> logger)
    {
        _pool = pool;
        _logger = logger;

        _ttl = TimeSpan.FromSeconds(
            int.TryParse(Environment.GetEnvironmentVariable("SOVRANT_SESSION_TTL_SECONDS"), out var ttl)
                ? ttl
                : 3600);

        _maxSessions = int.TryParse(
            Environment.GetEnvironmentVariable("SOVRANT_MAX_SESSIONS"), out var max)
            ? max
            : 500;
    }

    /// <summary>The configured session TTL.</summary>
    public TimeSpan Ttl => _ttl;

    /// <summary>The configured maximum active sessions.</summary>
    public int MaxSessions => _maxSessions;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(SweepInterval, stoppingToken).ConfigureAwait(false);

            var activeBefore = _pool.ActiveCount;
            var evicted = _pool.EvictExpired(_ttl, _maxSessions);

            if (evicted > 0)
                LogEvictionSweep(_logger, evicted, activeBefore, _pool.ActiveCount);
            else
                LogNoEviction(_logger, _pool.ActiveCount);
        }
    }
}
