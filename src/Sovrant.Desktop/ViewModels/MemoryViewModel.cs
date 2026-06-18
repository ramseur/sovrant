using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Memory;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Desktop.ViewModels;

public partial class MemoryViewModel : ViewModelBase
{
    private readonly IMemoryStore _memoryStore;
    private readonly IWorkspaceService _workspaceService;
    private string? _workspaceId;

    [ObservableProperty]
    private int _selectedTab;

    public bool IsNotesTab => SelectedTab == 0;
    public bool IsPatternsTab => SelectedTab == 1;
    public bool IsInstinctsTab => SelectedTab == 2;
    public bool IsSummariesTab => SelectedTab == 3;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsNotesTab));
        OnPropertyChanged(nameof(IsPatternsTab));
        OnPropertyChanged(nameof(IsInstinctsTab));
        OnPropertyChanged(nameof(IsSummariesTab));
    }

    [RelayCommand]
    private void SelectTab(string tab) => SelectedTab = int.Parse(tab, System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty] private int _patternCount;
    [ObservableProperty] private int _instinctCount;
    [ObservableProperty] private int _summaryCount;
    [ObservableProperty] private int _workspaceMemoryCount;

    [ObservableProperty] private string _statusMessage = string.Empty;

    // Add-form state for workspace memory
    [ObservableProperty] private string _newMemoryText = string.Empty;
    [ObservableProperty] private bool _newMemoryIsPrivate = true;

    public ObservableCollection<PatternItemViewModel> Patterns { get; } = [];
    public ObservableCollection<InstinctItemViewModel> Instincts { get; } = [];
    public ObservableCollection<SummaryItemViewModel> Summaries { get; } = [];
    public ObservableCollection<WorkspaceMemoryItemViewModel> WorkspaceMemories { get; } = [];

    public MemoryViewModel(IMemoryStore memoryStore, IWorkspaceService workspaceService)
    {
        _memoryStore = memoryStore;
        _workspaceService = workspaceService;
        _ = LoadAllAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAllAsync();

    [RelayCommand]
    private async Task DeletePatternAsync(PatternItemViewModel pattern)
    {
        try
        {
            await _memoryStore.RemovePatternAsync(pattern.Id, pattern.Project);
            Patterns.Remove(pattern);
            PatternCount = Patterns.Count;
            StatusMessage = $"Pattern '{pattern.Id}' deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to delete: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteInstinctAsync(InstinctItemViewModel instinct)
    {
        try
        {
            await _memoryStore.RemoveInstinctAsync(instinct.Id);
            Instincts.Remove(instinct);
            InstinctCount = Instincts.Count;
            StatusMessage = $"Instinct '{instinct.Id}' deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to delete: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddWorkspaceMemoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMemoryText) || string.IsNullOrEmpty(_workspaceId)) return;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var entry = new WorkspaceMemoryEntry
            {
                MemoryId = Guid.NewGuid().ToString("N"),
                WorkspaceId = _workspaceId,
                Layer = "note",
                Content = NewMemoryText.Trim(),
                OwnerUserId = App.SovrantUserId ?? string.Empty,
                IsPrivate = NewMemoryIsPrivate,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await _workspaceService.SaveMemoryAsync(entry);
            NewMemoryText = string.Empty;
            StatusMessage = "Memory saved.";
            await ReloadWorkspaceMemoriesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteWorkspaceMemoryAsync(WorkspaceMemoryItemViewModel item)
    {
        try
        {
            await _workspaceService.DeleteMemoryAsync(item.MemoryId);
            WorkspaceMemories.Remove(item);
            WorkspaceMemoryCount = WorkspaceMemories.Count;
            StatusMessage = "Memory deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to delete: {ex.Message}";
        }
    }

    private async Task ReloadWorkspaceMemoriesAsync()
    {
        if (string.IsNullOrEmpty(_workspaceId)) return;
        var entries = await _workspaceService.ListMemoryAsync(_workspaceId, viewerUserId: App.SovrantUserId);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkspaceMemories.Clear();
            foreach (var e in entries)
            {
                WorkspaceMemories.Add(new WorkspaceMemoryItemViewModel
                {
                    MemoryId = e.MemoryId,
                    Content = e.Content,
                    Layer = e.Layer,
                    IsPrivate = e.IsPrivate,
                    CreatedAt = e.CreatedAt,
                });
            }
            WorkspaceMemoryCount = WorkspaceMemories.Count;
        });
    }

    private async Task LoadAllAsync()
    {
        var project = Directory.GetCurrentDirectory();

        // Resolve personal workspace once per load cycle.
        if (_workspaceId is null)
        {
            var ws = await _workspaceService.GetPersonalAsync(App.SovrantUserId ?? string.Empty);
            _workspaceId = ws?.WorkspaceId;
        }

        var patterns = await _memoryStore.LoadPatternsAsync(project);
        var instincts = await _memoryStore.LoadInstinctsAsync();
        var summaries = await _memoryStore.LoadSummariesAsync(project, maxCount: 20);

        IReadOnlyList<WorkspaceMemoryEntry> workspaceEntries = [];
        if (!string.IsNullOrEmpty(_workspaceId))
            workspaceEntries = await _workspaceService.ListMemoryAsync(_workspaceId, viewerUserId: App.SovrantUserId);

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
            PatternCount = Patterns.Count;

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
            InstinctCount = Instincts.Count;

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
            SummaryCount = Summaries.Count;

            WorkspaceMemories.Clear();
            foreach (var e in workspaceEntries)
            {
                WorkspaceMemories.Add(new WorkspaceMemoryItemViewModel
                {
                    MemoryId = e.MemoryId,
                    Content = e.Content,
                    Layer = e.Layer,
                    IsPrivate = e.IsPrivate,
                    CreatedAt = e.CreatedAt,
                });
            }
            WorkspaceMemoryCount = WorkspaceMemories.Count;
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

public partial class WorkspaceMemoryItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _memoryId = string.Empty;
    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private string _layer = string.Empty;
    [ObservableProperty] private bool _isPrivate;
    [ObservableProperty] private DateTimeOffset _createdAt;
}
