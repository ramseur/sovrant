using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Memory;
using Sovrant.Runtime.Session;

namespace Sovrant.Desktop.ViewModels;

public partial class ActivityViewModel : ViewModelBase
{
    private readonly IMemoryStore _memoryStore;
    private readonly ISessionStore _sessionStore;
    private const int MaxOutputBytes = 2048;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private ActivityItemViewModel? _selectedSession;

    [ObservableProperty]
    private bool _detailLoading;

    [ObservableProperty]
    private string _detailError = string.Empty;

    public ObservableCollection<ActivityItemViewModel> Sessions { get; } = [];
    public ObservableCollection<TurnItemViewModel> Turns { get; } = [];

    public ActivityViewModel(IMemoryStore memoryStore, ISessionStore sessionStore)
    {
        _memoryStore = memoryStore;
        _sessionStore = sessionStore;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task SelectSessionAsync(ActivityItemViewModel? session)
    {
        SelectedSession = session;
        if (session is null) return;
        await LoadTurnsAsync(session.SessionId).ConfigureAwait(false);
    }

    private async Task LoadTurnsAsync(string sessionId)
    {
        DetailLoading = true;
        DetailError = string.Empty;
        await Dispatcher.UIThread.InvokeAsync(() => Turns.Clear());

        try
        {
            var entries = await _sessionStore.LoadAsync(sessionId, App.SovrantUserId).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var e in entries)
                {
                    Turns.Add(new TurnItemViewModel
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
            });
        }
        catch (Exception ex)
        {
            DetailError = $"Failed to load turns: {ex.Message}";
        }
        finally
        {
            DetailLoading = false;
        }
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        StatusMessage = string.Empty;

        try
        {
            var project = Directory.GetCurrentDirectory();
            var summaries = await _memoryStore.LoadSummariesAsync(project, maxCount: 50);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Sessions.Clear();
                foreach (var s in summaries)
                {
                    Sessions.Add(new ActivityItemViewModel
                    {
                        SessionId = s.SessionId,
                        Outcome = s.Outcome.ToString(),
                        StartedAt = s.StartedAt,
                        EndedAt = s.EndedAt,
                        Duration = FormatDuration(s.EndedAt - s.StartedAt),
                        TurnCount = s.TurnCount,
                        ErrorCount = s.ErrorCount,
                        ToolsUsed = string.Join(", ", s.ToolsUsed),
                        FilesModified = string.Join(", ", s.FilesModified),
                        FirstTask = s.Tasks.Count > 0 ? s.Tasks[0] : "(no task recorded)",
                        TotalTokens = s.TotalInputTokens + s.TotalOutputTokens,
                    });
                }
            });
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Failed to load activity: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
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
}

public partial class ActivityItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _sessionId = string.Empty;
    [ObservableProperty] private string _outcome = string.Empty;
    [ObservableProperty] private DateTimeOffset _startedAt;
    [ObservableProperty] private DateTimeOffset _endedAt;
    [ObservableProperty] private string _duration = string.Empty;
    [ObservableProperty] private int _turnCount;
    [ObservableProperty] private int _errorCount;
    [ObservableProperty] private string _toolsUsed = string.Empty;
    [ObservableProperty] private string _filesModified = string.Empty;
    [ObservableProperty] private string _firstTask = string.Empty;
    [ObservableProperty] private int _totalTokens;
}

public partial class TurnItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _role = string.Empty;
    [ObservableProperty] private string _roleClass = string.Empty;
    [ObservableProperty] private string _toolName = string.Empty;
    [ObservableProperty] private string _time = string.Empty;
    [ObservableProperty] private string _tokensLabel = string.Empty;
    [ObservableProperty] private bool _isError;
    [ObservableProperty] private string _content = string.Empty;
}
