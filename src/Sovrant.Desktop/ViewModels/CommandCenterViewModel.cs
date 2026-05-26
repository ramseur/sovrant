using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.CommandCenter;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Phase 90 / Phase 89 MVP — Command Center cockpit.
/// Polls the read-only aggregator every 2s and surfaces a flat grid
/// of "what is the engine doing right now?" rows. Agent-run drill-down
/// is rendered inline in this same view (focused mode) so /activity
/// doesn't need to exist as a separate destination.
/// </summary>
public sealed partial class CommandCenterViewModel : ViewModelBase, IDisposable
{
    private const int MaxOutputBytes = 2048;

    private readonly CommandCenterAggregator _aggregator;
    private readonly IAgentRunStore _agentRuns;
    private readonly ISessionStore _sessions;
    private readonly System.Timers.Timer _timer;
    private bool _disposed;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private int _activeMissions;
    [ObservableProperty] private int _activeTeamRuns;
    [ObservableProperty] private int _activeAgentRuns;
    [ObservableProperty] private int _activeSessions;
    [ObservableProperty] private int _activeClaws;
    [ObservableProperty] private string _lastRefreshed = string.Empty;
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _showHelp;

    // Focus-mode state. While IsFocused is true, the grid hides and the run
    // detail panel renders in its place; polling pauses so the detail view
    // doesn't get clobbered by a refresh.
    [ObservableProperty] private bool _isFocused;
    [ObservableProperty] private bool _focusLoading;
    [ObservableProperty] private string _focusError = string.Empty;
    [ObservableProperty] private string _focusedRunId = string.Empty;
    [ObservableProperty] private string _focusedTitle = string.Empty;
    [ObservableProperty] private string _focusedStatus = string.Empty;
    [ObservableProperty] private string _focusedOwner = string.Empty;
    [ObservableProperty] private string _focusedStarted = string.Empty;
    [ObservableProperty] private string _focusedDuration = string.Empty;
    [ObservableProperty] private string _focusedTokens = string.Empty;
    [ObservableProperty] private string _focusedCost = string.Empty;
    [ObservableProperty] private bool _focusedHasNoTurns;

    public ObservableCollection<CommandCenterTurnViewModel> FocusedTurns { get; } = [];

    public string HelpToggleLabel => ShowHelp ? "Hide guide" : "What are these?";

    partial void OnShowHelpChanged(bool value) => OnPropertyChanged(nameof(HelpToggleLabel));

    [RelayCommand]
    private void ToggleHelp() => ShowHelp = !ShowHelp;

    public ObservableCollection<CommandCenterRowViewModel> Rows { get; } = [];
    public ObservableCollection<CommandCenterRowViewModel> PagedRows { get; } = [];

    private const int PageSize = 10;
    [ObservableProperty] private int _currentPage;
    [ObservableProperty] private string _pageInfo = string.Empty;
    [ObservableProperty] private bool _hasPreviousPage;
    [ObservableProperty] private bool _hasNextPage;

    [RelayCommand] private void PreviousPage() { CurrentPage--; RefreshPagedRows(); }
    [RelayCommand] private void NextPage() { CurrentPage++; RefreshPagedRows(); }

    private void RefreshPagedRows()
    {
        PagedRows.Clear();
        foreach (var r in Rows.Skip(CurrentPage * PageSize).Take(PageSize))
            PagedRows.Add(r);
        int total = Rows.Count;
        HasPreviousPage = CurrentPage > 0;
        HasNextPage = (CurrentPage + 1) * PageSize < total;
        PageInfo = total <= PageSize ? string.Empty
            : $"{CurrentPage * PageSize + 1}–{Math.Min((CurrentPage + 1) * PageSize, total)} of {total}";
    }

    /// <summary>
    /// Raised when the user clicks a row in the cockpit grid. MainViewModel
    /// listens and routes by Kind: sessions resume in chat, team-runs open
    /// Orchestration, agent-runs stay on the cockpit and render inline via
    /// <see cref="OpenRunAsync"/>.
    /// </summary>
    public event EventHandler<CommandCenterRowSelectedEventArgs>? RowSelected;

    public CommandCenterViewModel(
        CommandCenterAggregator aggregator,
        IAgentRunStore agentRuns,
        ISessionStore sessions)
    {
        _aggregator = aggregator;
        _agentRuns = agentRuns;
        _sessions = sessions;
        _ = LoadAsync();
        _timer = new System.Timers.Timer(2000);
        _timer.Elapsed += async (_, _) => await LoadAsync();
        _timer.AutoReset = true;
        _timer.Start();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void SelectRow(CommandCenterRowViewModel? row)
    {
        if (row is null) return;
        RowSelected?.Invoke(this, new CommandCenterRowSelectedEventArgs(
            row.Kind, row.Id, row.DetailRoute));
    }

    [RelayCommand]
    private void BackToGrid()
    {
        IsFocused = false;
        FocusError = string.Empty;
        FocusedTurns.Clear();
    }

    /// <summary>
    /// Switch the cockpit into focused-detail mode for a single agent run.
    /// Pulls the run record from the ledger (header), then loads the run's
    /// session entries (turns). Caller is MainViewModel routing an agent-run
    /// click — the cockpit page itself stays current.
    /// </summary>
    public async Task OpenRunAsync(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId)) return;

        IsFocused = true;
        FocusLoading = true;
        FocusError = string.Empty;
        FocusedRunId = runId;
        FocusedTurns.Clear();
        FocusedHasNoTurns = false;

        try
        {
            var record = await _agentRuns.GetAsync(runId).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (record is null)
                {
                    FocusError = $"Run '{runId}' not found in the agent-run ledger.";
                    return;
                }

                FocusedTitle = record.MemberId ?? record.Kind;
                FocusedStatus = record.Status;
                FocusedOwner = record.UserId ?? string.Empty;
                FocusedStarted = record.StartedAt.LocalDateTime.ToString("MMM d, HH:mm:ss", CultureInfo.InvariantCulture);
                FocusedDuration = record.EndedAt is { } ended
                    ? FormatDuration(ended - record.StartedAt)
                    : "in flight";
                FocusedTokens = (record.InputTokens + record.OutputTokens) > 0
                    ? string.Format(CultureInfo.InvariantCulture, "in {0} / out {1}", record.InputTokens, record.OutputTokens)
                    : string.Empty;
                FocusedCost = record.CostUsd is { } c
                    ? c.ToString("C4", CultureInfo.InvariantCulture)
                    : string.Empty;
            });

            if (record is not null)
            {
                IReadOnlyList<SessionEntry> entries;
                try
                {
                    entries = await _sessions.LoadAsync(runId, App.SovrantUserId).ConfigureAwait(false);
                }
                catch
                {
                    entries = [];
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var e in entries)
                    {
                        FocusedTurns.Add(new CommandCenterTurnViewModel
                        {
                            Role = RoleLabel(e.Role),
                            RoleClass = RoleClass(e.Role),
                            ToolName = e.ToolName ?? string.Empty,
                            Time = e.Timestamp.LocalDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                            TokensLabel = (e.InputTokens + e.OutputTokens) > 0
                                ? string.Format(CultureInfo.InvariantCulture, "in {0} / out {1}", e.InputTokens, e.OutputTokens)
                                : string.Empty,
                            IsError = e.IsError,
                            Content = TruncateOutput(e.Content),
                        });
                    }
                    FocusedHasNoTurns = FocusedTurns.Count == 0;
                });
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => FocusError = $"Failed to load run: {ex.Message}");
        }
        finally
        {
            FocusLoading = false;
        }
    }

    private async Task LoadAsync()
    {
        // Don't refresh the grid while the user is reading focused detail —
        // the rolling clear/repopulate would clobber the panel underneath.
        if (IsFocused) return;

        try
        {
            IsLoading = true;
            var state = await _aggregator.GetActiveStateAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (IsFocused) return;
                ActiveMissions = state.ActiveMissions;
                ActiveTeamRuns = state.ActiveTeamRuns;
                ActiveAgentRuns = state.ActiveAgentRuns;
                ActiveSessions = state.ActiveSessions;
                ActiveClaws = state.ActiveClaws;
                LastRefreshed = state.GeneratedAt.LocalDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                CurrentPage = 0;
                Rows.Clear();
                foreach (var r in state.Rows)
                {
                    Rows.Add(new CommandCenterRowViewModel
                    {
                        Kind = r.Kind,
                        KindIcon = KindIcon(r.Kind),
                        Id = r.Id,
                        Title = r.Title,
                        Status = r.Status,
                        StartedAt = r.StartedAt,
                        LastActivity = FormatRelative(r.LastActivity),
                        Owner = r.OwnerLabel ?? string.Empty,
                        Preview = r.Preview ?? string.Empty,
                        Cost = r.CostUsd is null ? "—" : $"${r.CostUsd:F4}",
                        DetailRoute = r.DetailRoute ?? string.Empty,
                    });
                }
                IsEmpty = Rows.Count == 0;
                RefreshPagedRows();
                ErrorMessage = string.Empty;
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ErrorMessage = $"Failed to load cockpit state: {ex.Message}";
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string KindIcon(string kind) => kind switch
    {
        "mission" => "\U0001F3AF",
        "team-run" => "\U0001F465",
        "agent-run" => "\U0001F916",
        "session" => "\U0001F4AC",
        "claw" => "\U0001F517",
        _ => "•",
    };

    private static string FormatRelative(DateTimeOffset when)
    {
        var delta = DateTimeOffset.UtcNow - when;
        if (delta.TotalSeconds < 5) return "just now";
        if (delta.TotalSeconds < 60) return $"{(int)delta.TotalSeconds}s ago";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
        return when.LocalDateTime.ToString("MMM d, HH:mm", CultureInfo.InvariantCulture);
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalSeconds < 60) return $"{ts.TotalSeconds:F0}s";
        if (ts.TotalMinutes < 60) return $"{ts.Minutes}m {ts.Seconds}s";
        return $"{(int)ts.TotalHours}h {ts.Minutes}m";
    }

    private static string TruncateOutput(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= MaxOutputBytes ? s : s[..MaxOutputBytes] + "\n…(truncated)";
    }

    private static string RoleLabel(string role) => role switch
    {
        "user" => "User",
        "assistant" => "Assistant",
        "tool_use" => "Tool",
        "tool_result" => "Tool result",
        "system" => "System",
        _ => role,
    };

    private static string RoleClass(string role) => role switch
    {
        "user" => "user",
        "assistant" => "assistant",
        "tool_use" or "tool_result" => "tool",
        _ => "system",
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }
}

public partial class CommandCenterRowViewModel : ViewModelBase
{
    [ObservableProperty] private string _kind = string.Empty;
    [ObservableProperty] private string _kindIcon = string.Empty;
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private DateTimeOffset _startedAt;
    [ObservableProperty] private string _lastActivity = string.Empty;
    [ObservableProperty] private string _owner = string.Empty;
    [ObservableProperty] private string _preview = string.Empty;
    [ObservableProperty] private string _cost = string.Empty;
    [ObservableProperty] private string _detailRoute = string.Empty;
}

public partial class CommandCenterTurnViewModel : ViewModelBase
{
    [ObservableProperty] private string _role = string.Empty;
    [ObservableProperty] private string _roleClass = string.Empty;
    [ObservableProperty] private string _toolName = string.Empty;
    [ObservableProperty] private string _time = string.Empty;
    [ObservableProperty] private string _tokensLabel = string.Empty;
    [ObservableProperty] private bool _isError;
    [ObservableProperty] private string _content = string.Empty;
}

public sealed class CommandCenterRowSelectedEventArgs : EventArgs
{
    public CommandCenterRowSelectedEventArgs(string kind, string id, string detailRoute)
    {
        Kind = kind;
        Id = id;
        DetailRoute = detailRoute;
    }

    public string Kind { get; }
    public string Id { get; }
    public string DetailRoute { get; }
}
