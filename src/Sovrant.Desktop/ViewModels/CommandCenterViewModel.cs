using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.CommandCenter;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Phase 90 / Phase 89 MVP — Command Center cockpit.
/// Polls the read-only aggregator every 2s and surfaces a flat grid
/// of "what is the engine doing right now?" rows.
/// </summary>
public sealed partial class CommandCenterViewModel : ViewModelBase, IDisposable
{
    private readonly CommandCenterAggregator _aggregator;
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

    public string HelpToggleLabel => ShowHelp ? "Hide guide" : "What are these?";

    partial void OnShowHelpChanged(bool value) => OnPropertyChanged(nameof(HelpToggleLabel));

    [RelayCommand]
    private void ToggleHelp() => ShowHelp = !ShowHelp;

    public ObservableCollection<CommandCenterRowViewModel> Rows { get; } = [];

    public CommandCenterViewModel(CommandCenterAggregator aggregator)
    {
        _aggregator = aggregator;
        _ = LoadAsync();
        _timer = new System.Timers.Timer(2000);
        _timer.Elapsed += async (_, _) => await LoadAsync();
        _timer.AutoReset = true;
        _timer.Start();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            var state = await _aggregator.GetActiveStateAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ActiveMissions = state.ActiveMissions;
                ActiveTeamRuns = state.ActiveTeamRuns;
                ActiveAgentRuns = state.ActiveAgentRuns;
                ActiveSessions = state.ActiveSessions;
                ActiveClaws = state.ActiveClaws;
                LastRefreshed = state.GeneratedAt.LocalDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

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
                        Preview = r.Preview ?? string.Empty,
                        Cost = r.CostUsd is null ? "—" : $"${r.CostUsd:F4}",
                        DetailRoute = r.DetailRoute ?? string.Empty,
                    });
                }
                IsEmpty = Rows.Count == 0;
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
    [ObservableProperty] private string _preview = string.Empty;
    [ObservableProperty] private string _cost = string.Empty;
    [ObservableProperty] private string _detailRoute = string.Empty;
}
