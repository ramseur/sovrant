using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Memory;

namespace Sovrant.Desktop.ViewModels;

public partial class ActivityViewModel : ViewModelBase
{
    private readonly IMemoryStore _memoryStore;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public ObservableCollection<ActivityItemViewModel> Sessions { get; } = [];

    public ActivityViewModel(IMemoryStore memoryStore)
    {
        _memoryStore = memoryStore;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

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
