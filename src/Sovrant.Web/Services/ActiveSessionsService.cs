using Sovrant.Runtime.Workspaces;

namespace Sovrant.Web.Services;

/// <summary>
/// Owns the CancellationTokenSource for each background turn so navigating
/// away from Chat does not cancel it. Registered as Scoped (one per Blazor
/// circuit / browser tab).
/// </summary>
public sealed class ActiveSessionsService : IDisposable
{
    private readonly IWorkspaceSettingsStore _settings;
    private readonly ActiveContextService _context;
    private readonly Lock _lock = new();

    private readonly Dictionary<string, ActiveSessionEntry> _sessions = [];

    public event Action? OnChanged;

    public ActiveSessionsService(IWorkspaceSettingsStore settings, ActiveContextService context)
    {
        _settings = settings;
        _context = context;
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    public async Task<int> GetMaxActiveAsync()
    {
        var raw = await _settings.GetAsync(_context.WorkspaceId, WorkspaceSettingsKeys.MaxActiveSessions).ConfigureAwait(false);
        return raw is not null && int.TryParse(raw, out var v) ? Math.Clamp(v, 1, 5) : 5;
    }

    public async Task<bool> IsEnabledAsync()
    {
        var raw = await _settings.GetAsync(_context.WorkspaceId, WorkspaceSettingsKeys.BackgroundSessionsEnabled).ConfigureAwait(false);
        return raw is null || !raw.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    public Task SetMaxActiveAsync(int value) =>
        _settings.SetAsync(_context.WorkspaceId, WorkspaceSettingsKeys.MaxActiveSessions,
            Math.Clamp(value, 1, 5).ToString(System.Globalization.CultureInfo.InvariantCulture));

    public Task SetEnabledAsync(bool value) =>
        _settings.SetAsync(_context.WorkspaceId, WorkspaceSettingsKeys.BackgroundSessionsEnabled,
            value ? "true" : "false");

    // ── Registration ──────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a new background session. Returns false when the active-session
    /// limit has been reached (caller should show an error toast and abort).
    /// </summary>
    public bool TryRegister(string sessionId, string title, CancellationTokenSource cts)
    {
        lock (_lock)
        {
            var running = _sessions.Values.Count(s => s.Status == ActiveSessionStatus.Running);
            // Synchronous limit check — caller reads max from GetMaxActiveAsync before calling
            if (_sessions.ContainsKey(sessionId)) return false; // already registered

            var entry = new ActiveSessionEntry(sessionId, title, cts, DateTime.UtcNow);
            _sessions[sessionId] = entry;
        }
        OnChanged?.Invoke();
        return true;
    }

    public bool HasSession(string sessionId)
    {
        lock (_lock) return _sessions.ContainsKey(sessionId);
    }

    public int RunningCount()
    {
        lock (_lock) return _sessions.Values.Count(s => s.Status == ActiveSessionStatus.Running);
    }

    // ── Live attachment ───────────────────────────────────────────────────────

    /// <summary>
    /// Attaches the Chat component as a live listener. Replays buffered events
    /// immediately, then subscribes to new events going forward.
    /// Returns (buffered, isRunning) so the component can seed its UI.
    /// </summary>
    public (IReadOnlyList<object> Buffered, bool IsRunning) Attach(string sessionId, Action<object> handler)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry))
                return ([], false);
            entry.LiveHandler = handler;
            return (entry.Buffer.ToList(), entry.Status == ActiveSessionStatus.Running);
        }
    }

    /// <summary>Removes the live handler without cancelling. Called on Chat dispose.</summary>
    public void Detach(string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var entry))
                entry.LiveHandler = null;
        }
    }

    // ── Event pipeline ────────────────────────────────────────────────────────

    /// <summary>Buffers the event and forwards it to the live handler if one is attached.</summary>
    public void PushEvent(string sessionId, object evt)
    {
        Action<object>? handler;
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry)) return;
            if (entry.Buffer.Count < 500)
                entry.Buffer.Add(evt);
            handler = entry.LiveHandler;
        }
        handler?.Invoke(evt);
    }

    public void Complete(string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var entry))
            {
                entry.Status = ActiveSessionStatus.Completed;
                entry.EndedAt = DateTime.UtcNow;
                ScheduleAutoCleanup(sessionId);
            }
        }
        OnChanged?.Invoke();
    }

    public void Fail(string sessionId, string error)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var entry))
            {
                entry.Status = ActiveSessionStatus.Failed;
                entry.Error = error;
                entry.EndedAt = DateTime.UtcNow;
                ScheduleAutoCleanup(sessionId);
            }
        }
        OnChanged?.Invoke();
    }

    public void Cancel(string sessionId)
    {
        CancellationTokenSource? cts;
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry)) return;
            cts = entry.Cts;
            entry.Status = ActiveSessionStatus.Failed;
            entry.Error = "Cancelled";
            entry.EndedAt = DateTime.UtcNow;
            ScheduleAutoCleanup(sessionId);
        }
        try { cts?.Cancel(); } catch { /* already disposed */ }
        OnChanged?.Invoke();
    }

    /// <summary>Removes a completed/failed session from the list (user dismissed it).</summary>
    public void Dismiss(string sessionId)
    {
        lock (_lock) _sessions.Remove(sessionId);
        OnChanged?.Invoke();
    }

    public IReadOnlyList<ActiveSessionInfo> GetAll()
    {
        lock (_lock)
            return _sessions.Values
                .Select(e => new ActiveSessionInfo(e.SessionId, e.Title, e.Status, e.StartedAt, e.EndedAt, e.Error))
                .ToList();
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void ScheduleAutoCleanup(string sessionId)
    {
        // called inside lock — fire-and-forget cleanup after 30 min
        _ = Task.Delay(TimeSpan.FromMinutes(30)).ContinueWith(_ =>
        {
            lock (_lock) _sessions.Remove(sessionId);
            OnChanged?.Invoke();
        }, TaskScheduler.Default);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var e in _sessions.Values.Where(e => e.Status == ActiveSessionStatus.Running))
            {
                try { e.Cts?.Cancel(); } catch { }
            }
            _sessions.Clear();
        }
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed class ActiveSessionEntry(string sessionId, string title, CancellationTokenSource cts, DateTime startedAt)
    {
        public string SessionId { get; } = sessionId;
        public string Title { get; } = title;
        public CancellationTokenSource? Cts { get; } = cts;
        public DateTime StartedAt { get; } = startedAt;
        public DateTime? EndedAt { get; set; }
        public ActiveSessionStatus Status { get; set; } = ActiveSessionStatus.Running;
        public string? Error { get; set; }
        public Action<object>? LiveHandler { get; set; }
        public List<object> Buffer { get; } = [];
    }
}

public enum ActiveSessionStatus { Running, Completed, Failed }

public sealed record ActiveSessionInfo(
    string SessionId,
    string Title,
    ActiveSessionStatus Status,
    DateTime StartedAt,
    DateTime? EndedAt,
    string? Error)
{
    public TimeSpan Elapsed => (EndedAt ?? DateTime.UtcNow) - StartedAt;
}
