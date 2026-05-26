using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.UserDashboard;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Phase 98 — per-user cross-workspace activity view. Polls the
/// <see cref="UserDashboardAggregator"/> every 2s and renders the rows
/// the signed-in user is allowed to see (own private + own public +
/// teammates' public in shared workspaces).
/// </summary>
public sealed partial class UserDashboardViewModel : ViewModelBase, IDisposable
{
    private readonly UserDashboardAggregator _aggregator;
    private readonly IPrincipalAccessor _principal;
    private readonly System.Timers.Timer _timer;
    private bool _disposed;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private int _ownMissions;
    [ObservableProperty] private int _ownTeamRuns;
    [ObservableProperty] private int _ownAgentRuns;
    [ObservableProperty] private int _ownSessions;
    [ObservableProperty] private int _othersPublicRows;
    [ObservableProperty] private int _claws;
    [ObservableProperty] private string _lastRefreshed = string.Empty;
    [ObservableProperty] private bool _isEmpty = true;

    public ObservableCollection<UserDashboardRowViewModel> Rows { get; } = [];

    public UserDashboardViewModel(UserDashboardAggregator aggregator, IPrincipalAccessor principal)
    {
        _aggregator = aggregator;
        _principal = principal;
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
        var userId = _principal.UserId;
        if (string.IsNullOrEmpty(userId)) return;

        try
        {
            IsLoading = true;
            var state = await _aggregator.GetStateAsync(userId).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                OwnMissions = state.OwnMissions;
                OwnTeamRuns = state.OwnTeamRuns;
                OwnAgentRuns = state.OwnAgentRuns;
                OwnSessions = state.OwnSessions;
                OthersPublicRows = state.OthersPublicRows;
                Claws = state.Claws;
                LastRefreshed = state.GeneratedAt.LocalDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

                Rows.Clear();
                foreach (var r in state.Rows)
                {
                    Rows.Add(new UserDashboardRowViewModel
                    {
                        Kind = r.Kind,
                        KindIcon = KindIcon(r.Kind),
                        Id = r.Id,
                        Title = r.Title,
                        Status = r.Status,
                        LastActivity = FormatRelative(r.LastActivity),
                        Owner = r.OwnerLabel ?? string.Empty,
                        Preview = r.Preview ?? string.Empty,
                        Cost = r.CostUsd is null ? "—" : $"${r.CostUsd:F4}",
                        IsOwn = r.IsOwn,
                        IsPrivate = r.IsPrivate,
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
                ErrorMessage = $"Failed to load dashboard: {ex.Message}";
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

public partial class UserDashboardRowViewModel : ViewModelBase
{
    [ObservableProperty] private string _kind = string.Empty;
    [ObservableProperty] private string _kindIcon = string.Empty;
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _lastActivity = string.Empty;
    [ObservableProperty] private string _owner = string.Empty;
    [ObservableProperty] private string _preview = string.Empty;
    [ObservableProperty] private string _cost = string.Empty;
    [ObservableProperty] private bool _isOwn;
    [ObservableProperty] private bool _isPrivate;
}
