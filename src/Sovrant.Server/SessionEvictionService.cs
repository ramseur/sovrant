using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Server;

/// <summary>
/// Background service that periodically evicts idle sessions from the <see cref="IRuntimeSessionPool"/>
/// based on TTL and max-sessions cap. Runs every 5 minutes.
///
/// TTL and cap are loaded from the global <c>workspace_settings</c> row;
/// env vars <c>SOVRANT_SESSION_TTL_SECONDS</c> / <c>SOVRANT_MAX_SESSIONS</c>
/// still win when set.
/// </summary>
internal sealed partial class SessionEvictionService : BackgroundService
{
    private const int DefaultTtlSeconds = 3600;
    private const int DefaultMaxSessions = 500;
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
        IWorkspaceSettingsStore settings,
        ILogger<SessionEvictionService> logger)
    {
        _pool = pool;
        _logger = logger;

        var ttlSeconds = ResolveInt(settings, WorkspaceSettingsKeys.SessionTtlSeconds,
            "SOVRANT_SESSION_TTL_SECONDS", DefaultTtlSeconds);
        _ttl = TimeSpan.FromSeconds(ttlSeconds);

        _maxSessions = ResolveInt(settings, WorkspaceSettingsKeys.MaxSessions,
            "SOVRANT_MAX_SESSIONS", DefaultMaxSessions);
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

    private static int ResolveInt(IWorkspaceSettingsStore settings, string settingsKey, string envVar, int fallback)
    {
        if (int.TryParse(Environment.GetEnvironmentVariable(envVar), out var fromEnv))
            return fromEnv;

        var fromDb = settings.GetGlobalAsync(settingsKey).GetAwaiter().GetResult();
        if (int.TryParse(fromDb, out var parsed))
            return parsed;

        return fallback;
    }
}
