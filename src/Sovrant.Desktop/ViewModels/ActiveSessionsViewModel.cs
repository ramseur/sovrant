using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Desktop mirror of the Web ActiveSessionsService. Singleton — survives
/// navigation so background turns keep running after the user leaves Chat.
/// </summary>
public sealed partial class ActiveSessionsViewModel : ObservableObject, IDisposable
{
    private readonly IWorkspaceSettingsStore _settings;
    private readonly ActiveContextViewModel _context;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, ActiveSessionEntry> _sessions = [];

    public ObservableCollection<ActiveSessionInfoViewModel> ActiveSessions { get; } = [];

    public bool HasActiveSessions => ActiveSessions.Count > 0;

    public ActiveSessionsViewModel(IWorkspaceSettingsStore settings, ActiveContextViewModel context)
    {
        _settings = settings;
        _context = context;
        ActiveSessions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasActiveSessions));
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    public async Task<int> GetMaxActiveAsync()
    {
        var raw = await _settings.GetAsync(_context.ActiveWorkspaceId, WorkspaceSettingsKeys.MaxActiveSessions).ConfigureAwait(false);
        return raw is not null && int.TryParse(raw, out var v) ? Math.Clamp(v, 1, 5) : 5;
    }

    public async Task<bool> IsEnabledAsync()
    {
        var raw = await _settings.GetAsync(_context.ActiveWorkspaceId, WorkspaceSettingsKeys.BackgroundSessionsEnabled).ConfigureAwait(false);
        return raw is null || !raw.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    public Task SetMaxActiveAsync(int value) =>
        _settings.SetAsync(_context.ActiveWorkspaceId, WorkspaceSettingsKeys.MaxActiveSessions,
            Math.Clamp(value, 1, 5).ToString(System.Globalization.CultureInfo.InvariantCulture));

    public Task SetEnabledAsync(bool value) =>
        _settings.SetAsync(_context.ActiveWorkspaceId, WorkspaceSettingsKeys.BackgroundSessionsEnabled,
            value ? "true" : "false");

    // ── Registration ──────────────────────────────────────────────────────────

    public bool TryRegister(string sessionId, string title, CancellationTokenSource cts)
    {
        lock (_lock)
        {
            if (_sessions.ContainsKey(sessionId)) return false;
            var entry = new ActiveSessionEntry(sessionId, title, cts, DateTime.UtcNow);
            _sessions[sessionId] = entry;
        }
        RefreshObservable();
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

    public void Detach(string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var entry))
                entry.LiveHandler = null;
        }
    }

    // ── Event pipeline ────────────────────────────────────────────────────────

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
        RefreshObservable();
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
        RefreshObservable();
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
        try { cts?.Cancel(); } catch { }
        RefreshObservable();
    }

    public void Dismiss(string sessionId)
    {
        lock (_lock) _sessions.Remove(sessionId);
        RefreshObservable();
    }

    public IReadOnlyList<ActiveSessionInfo> GetAll()
    {
        lock (_lock)
            return _sessions.Values
                .Select(e => new ActiveSessionInfo(e.SessionId, e.Title, e.Status, e.StartedAt, e.EndedAt, e.Error))
                .ToList();
    }

    // ── Observable sync ───────────────────────────────────────────────────────

    private void RefreshObservable()
    {
        Dispatcher.UIThread.Post(() =>
        {
            List<ActiveSessionInfo> snapshot;
            lock (_lock)
                snapshot = _sessions.Values
                    .Select(e => new ActiveSessionInfo(e.SessionId, e.Title, e.Status, e.StartedAt, e.EndedAt, e.Error))
                    .ToList();

            // Remove stale
            for (int i = ActiveSessions.Count - 1; i >= 0; i--)
            {
                var vm = ActiveSessions[i];
                if (!snapshot.Any(s => s.SessionId == vm.SessionId))
                    ActiveSessions.RemoveAt(i);
            }

            // Add new / update existing
            foreach (var info in snapshot)
            {
                var existing = ActiveSessions.FirstOrDefault(v => v.SessionId == info.SessionId);
                if (existing is null)
                    ActiveSessions.Add(new ActiveSessionInfoViewModel(info, this));
                else
                    existing.Update(info);
            }
        });
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void ScheduleAutoCleanup(string sessionId)
    {
        _ = Task.Delay(TimeSpan.FromMinutes(30)).ContinueWith(_ =>
        {
            lock (_lock) _sessions.Remove(sessionId);
            RefreshObservable();
        }, TaskScheduler.Default);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var e in _sessions.Values.Where(e => e.Status == ActiveSessionStatus.Running))
                try { e.Cts?.Cancel(); } catch { }
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

public partial class ActiveSessionInfoViewModel : ObservableObject
{
    private readonly ActiveSessionsViewModel _owner;

    public string SessionId { get; }

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private ActiveSessionStatus _status;
    [ObservableProperty] private DateTime _startedAt;
    [ObservableProperty] private DateTime? _endedAt;
    [ObservableProperty] private string? _error;

    public bool IsRunning => Status == ActiveSessionStatus.Running;
    public string StatusIcon => Status switch
    {
        ActiveSessionStatus.Running => "⟳",
        ActiveSessionStatus.Completed => "✓",
        ActiveSessionStatus.Failed => "✗",
        _ => "",
    };
    public TimeSpan Elapsed => (EndedAt ?? DateTime.UtcNow) - StartedAt;
    public string ElapsedDisplay => Elapsed.TotalSeconds < 60
        ? $"{(int)Elapsed.TotalSeconds}s"
        : $"{(int)Elapsed.TotalMinutes}m {Elapsed.Seconds}s";

    public ActiveSessionInfoViewModel(ActiveSessionInfo info, ActiveSessionsViewModel owner)
    {
        _owner = owner;
        SessionId = info.SessionId;
        Update(info);
    }

    public void Update(ActiveSessionInfo info)
    {
        Title = info.Title;
        Status = info.Status;
        StartedAt = info.StartedAt;
        EndedAt = info.EndedAt;
        Error = info.Error;
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(Elapsed));
        OnPropertyChanged(nameof(ElapsedDisplay));
    }

    public void Cancel() => _owner.Cancel(SessionId);
    public void Dismiss() => _owner.Dismiss(SessionId);
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
