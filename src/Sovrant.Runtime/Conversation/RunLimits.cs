using System.Globalization;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Phase 145 A8.1 — the longest any run may take: a chat reply, a workflow run, a swarm, a scheduled
/// job. 2 hours by default; admins set it in the Sign-in section of Users → Registration &amp; sign-in.
/// It caps compute, energy and spend for work nobody is watching (the USD budgets cap money).
/// Read from the global settings first, then <see cref="MaxRunMinutesVariable"/>, then the default.
/// </summary>
public static class RunLimits
{
    public const string MaxRunMinutesVariable = "SOVRANT_MAX_RUN_MINUTES";
    public const int DefaultMinutes = 120;
    public static IReadOnlyList<int> MinuteChoices { get; } = [15, 30, 60, 120, 240, 480];

    public static TimeSpan Load(IWorkspaceSettingsStore? settings, Func<string, string?>? env = null)
    {
        env ??= Environment.GetEnvironmentVariable;
        var fromEnv = Positive(env(MaxRunMinutesVariable));
        // Phase 148: with SOVRANT_ENV_OVERRIDE on, the variable wins (the app shows it locked).
        if (Config.EnvOverride.IsOn(env) && fromEnv is { } locked) return TimeSpan.FromMinutes(locked);
        if (Positive(settings is null ? null : Read(settings)) is { } s) return TimeSpan.FromMinutes(s);
        if (fromEnv is { } e) return TimeSpan.FromMinutes(e);
        return TimeSpan.FromMinutes(DefaultMinutes);
    }

    public static Task SaveAsync(IWorkspaceSettingsStore store, int minutes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentOutOfRangeException.ThrowIfLessThan(minutes, 1);
        return store.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, WorkspaceSettingsKeys.RunMaxMinutes,
            minutes.ToString(CultureInfo.InvariantCulture), ct);
    }

    /// <summary>What a stopped run says, in the conversation or the workflow's history.</summary>
    public static string StoppedMessage(TimeSpan limit) =>
        $"Stopped after {WebSignInSettings.Describe(limit)} — the time limit set by your admin. What finished before then is kept.";

    private static string? Read(IWorkspaceSettingsStore settings)
    {
        try { return settings.GetGlobalAsync(WorkspaceSettingsKeys.RunMaxMinutes).GetAwaiter().GetResult(); }
        catch (Exception ex) when (ex is InvalidOperationException or System.Data.Common.DbException) { return null; }
    }

    private static int? Positive(string? raw) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;
}

/// <summary>
/// The time left for the current chat reply (Phase 145 A8.1): the per-reply timeout and the run limit,
/// on one cancellation source. Waiting for the person to answer an approval doesn't use up the
/// per-reply timeout (they may be away), but does count toward the run limit.
/// Carried in async-local state for the reply, like <see cref="TurnContext"/>.
/// </summary>
public sealed class RunDeadline : IDisposable
{
    private static readonly AsyncLocal<RunDeadline?> s_current = new();

    private readonly CancellationTokenSource _cts;
    private readonly TimeSpan _turnTimeout;
    private readonly TimeProvider _clock;
    private readonly DateTimeOffset _runEnds;
    private readonly RunDeadline? _previous;
    private readonly Lock _gate = new();
    private TimeSpan _turnUsed;
    private DateTimeOffset _activeSince;
    private int _pauses;

    public static RunDeadline? Current => s_current.Value;

    /// <summary>The run limit this reply is held to.</summary>
    public TimeSpan RunLimit { get; }

    /// <summary>True once the run limit (rather than the per-reply timeout) has been reached.</summary>
    public bool RunLimitReached => _clock.GetUtcNow() >= _runEnds;

    private RunDeadline(CancellationTokenSource cts, TimeSpan turnTimeout, TimeSpan runLimit, TimeProvider clock)
    {
        _cts = cts;
        _turnTimeout = turnTimeout;
        RunLimit = runLimit;
        _clock = clock;
        _activeSince = clock.GetUtcNow();
        _runEnds = _activeSince + runLimit;
        _previous = s_current.Value;
        Arm();
    }

    /// <summary>Starts the deadline for a reply and makes it current until disposed.</summary>
    public static RunDeadline Begin(CancellationTokenSource cts, TimeSpan turnTimeout, TimeSpan runLimit, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(cts);
        var d = new RunDeadline(cts, turnTimeout, runLimit, clock ?? TimeProvider.System);
        s_current.Value = d;
        return d;
    }

    /// <summary>While disposed-not-yet, the per-reply timeout is paused (only the run limit applies).</summary>
    public IDisposable PauseForApproval()
    {
        lock (_gate)
        {
            if (_pauses++ == 0)
                _turnUsed += _clock.GetUtcNow() - _activeSince;
            Arm();
        }
        return new Resume(this);
    }

    private void ResumeTurn()
    {
        lock (_gate)
        {
            if (--_pauses == 0)
                _activeSince = _clock.GetUtcNow();
            Arm();
        }
    }

    private void Arm()
    {
        var now = _clock.GetUtcNow();
        var runLeft = _runEnds - now;
        var left = _pauses > 0 ? runLeft : Min(runLeft, _turnTimeout - _turnUsed - (now - _activeSince));
        try { _cts.CancelAfter(left > TimeSpan.Zero ? left : TimeSpan.FromMilliseconds(1)); }
        catch (ObjectDisposedException) { /* the reply already finished */ }
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    public void Dispose() => s_current.Value = _previous;

    private sealed class Resume(RunDeadline owner) : IDisposable
    {
        private int _done;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0) owner.ResumeTurn();
        }
    }
}
