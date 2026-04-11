using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Memory;

namespace Sovrant.Desktop.ViewModels;

public partial class MemoryViewModel : ViewModelBase
{
    private readonly IMemoryStore _memoryStore;

    [ObservableProperty]
    private string _selectedTab = "Patterns";

    public ObservableCollection<PatternItemViewModel> Patterns { get; } = [];
    public ObservableCollection<InstinctItemViewModel> Instincts { get; } = [];
    public ObservableCollection<SummaryItemViewModel> Summaries { get; } = [];

    public MemoryViewModel(IMemoryStore memoryStore)
    {
        _memoryStore = memoryStore;
        _ = LoadAllAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAllAsync();

    private async Task LoadAllAsync()
    {
        var project = Directory.GetCurrentDirectory();

        var patterns = await _memoryStore.LoadPatternsAsync(project);
        var instincts = await _memoryStore.LoadInstinctsAsync();
        var summaries = await _memoryStore.LoadSummariesAsync(project, maxCount: 20);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Patterns.Clear();
            foreach (var p in patterns)
            {
                Patterns.Add(new PatternItemViewModel
                {
                    Id = p.Id,
                    Pattern = p.Pattern,
                    Confidence = p.Confidence,
                    Project = p.Project,
                });
            }

            Instincts.Clear();
            foreach (var i in instincts)
            {
                Instincts.Add(new InstinctItemViewModel
                {
                    Id = i.Id,
                    Trigger = i.Trigger,
                    Action = i.Action,
                    Confidence = i.Confidence,
                });
            }

            Summaries.Clear();
            foreach (var s in summaries)
            {
                Summaries.Add(new SummaryItemViewModel
                {
                    SessionId = s.SessionId,
                    Outcome = s.Outcome.ToString(),
                    TurnCount = s.TurnCount,
                    ToolsUsed = string.Join(", ", s.ToolsUsed),
                    StartedAt = s.StartedAt,
                });
            }
        });
    }
}

public partial class PatternItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _pattern = string.Empty;
    [ObservableProperty] private double _confidence;
    [ObservableProperty] private string _project = string.Empty;
}

public partial class InstinctItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _trigger = string.Empty;
    [ObservableProperty] private string _action = string.Empty;
    [ObservableProperty] private double _confidence;
}

public partial class SummaryItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _sessionId = string.Empty;
    [ObservableProperty] private string _outcome = string.Empty;
    [ObservableProperty] private int _turnCount;
    [ObservableProperty] private string _toolsUsed = string.Empty;
    [ObservableProperty] private DateTimeOffset _startedAt;
}
