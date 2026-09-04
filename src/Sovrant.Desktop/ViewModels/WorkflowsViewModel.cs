using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Workflows;

namespace Sovrant.Desktop.ViewModels;

public partial class WorkflowsViewModel : ViewModelBase
{
    private readonly IWorkflowStore _store;
    private readonly IWorkflowExecutor _executor;
    private readonly WorkflowExportService _exporter;
    private readonly ActiveContextViewModel _activeContext;

    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };

    [ObservableProperty] private int _workflowCount;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private WorkflowItemViewModel? _selectedWorkflow;

    [ObservableProperty] private bool _isCreating;
    [ObservableProperty] private string _createGoal = string.Empty;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _showExport;
    [ObservableProperty] private string _exportText = string.Empty;

    public bool HasSelection => SelectedWorkflow is not null && !IsCreating;
    public bool HasNoSelection => SelectedWorkflow is null && !IsCreating;
    public string RunButtonLabel =>
        IsRunning ? "Running…" : SelectedWorkflow?.Status == WorkflowStatus.AwaitingHuman ? "Resume" : "Run now";

    public ObservableCollection<WorkflowItemViewModel> Workflows { get; } = [];

    public WorkflowsViewModel(
        IWorkflowStore store,
        IWorkflowExecutor executor,
        WorkflowExportService exporter,
        ActiveContextViewModel activeContext)
    {
        _store = store;
        _executor = executor;
        _exporter = exporter;
        _activeContext = activeContext;
        LoadAll();
    }

    // ── Commands ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void Refresh() => LoadAll();

    [RelayCommand]
    private void SelectWorkflow(WorkflowItemViewModel workflow)
    {
        IsCreating = false;
        ShowExport = false;
        SelectedWorkflow = workflow;
    }

    // ── Create ───────────────────────────────────────────────────────────

    [RelayCommand]
    private void StartCreate()
    {
        IsCreating = true;
        SelectedWorkflow = null;
        ShowExport = false;
        CreateGoal = string.Empty;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void CancelCreate() => IsCreating = false;

    [RelayCommand]
    private async Task ConfirmCreateAsync()
    {
        var goal = CreateGoal.Trim();
        if (string.IsNullOrEmpty(goal)) return;

        var workflow = await _store.CreateAsync(
            goal,
            workspaceId: _activeContext.ActiveWorkspaceId,
            projectId: string.IsNullOrEmpty(_activeContext.ActiveProjectId) ? null : _activeContext.ActiveProjectId,
            ownerUserId: App.SovrantUserId).ConfigureAwait(true);

        IsCreating = false;
        StatusMessage = "Workflow created — the scheduler will pick it up on its next poll tick.";
        LoadAll();
        SelectedWorkflow = Workflows.FirstOrDefault(w => w.Id == workflow.Id);
        if (SelectedWorkflow is not null)
            await LoadDetailAsync(SelectedWorkflow).ConfigureAwait(true);
    }

    // ── Actions ──────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanRunNow))]
    private async Task RunNowAsync()
    {
        if (SelectedWorkflow is null || IsRunning) return;

        IsRunning = true;
        try
        {
            await _executor.RunAsync(SelectedWorkflow.Id).ConfigureAwait(true);
            StatusMessage = "Workflow advanced.";
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => StatusMessage = $"Run failed: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            LoadAll();
        }
    }

    private bool CanRunNow() => SelectedWorkflow is not null && !IsTerminal(SelectedWorkflow.Status) && !IsRunning;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelWorkflowAsync()
    {
        if (SelectedWorkflow is null || IsTerminal(SelectedWorkflow.Status)) return;

        await _store.AppendEventAsync(SelectedWorkflow.Id, WorkflowEventTypes.Cancelled, "{}").ConfigureAwait(true);
        await _store.UpdateStateAsync(SelectedWorkflow.Id, WorkflowStatus.Cancelled, completedAt: DateTimeOffset.UtcNow).ConfigureAwait(true);
        StatusMessage = "Workflow cancelled.";
        LoadAll();
    }

    private bool CanCancel() => SelectedWorkflow is not null && !IsTerminal(SelectedWorkflow.Status);

    [RelayCommand]
    private async Task ToggleExportAsync()
    {
        if (SelectedWorkflow is null) return;
        ShowExport = !ShowExport;
        if (ShowExport)
            ExportText = await _exporter.ExportMarkdownAsync(SelectedWorkflow.Id).ConfigureAwait(true);
    }

    partial void OnSelectedWorkflowChanged(WorkflowItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSelection));
        OnPropertyChanged(nameof(RunButtonLabel));
        RunNowCommand.NotifyCanExecuteChanged();
        CancelWorkflowCommand.NotifyCanExecuteChanged();
        _ = value is not null ? LoadDetailAsync(value) : Task.CompletedTask;
    }

    partial void OnIsCreatingChanged(bool value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSelection));
    }

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(RunButtonLabel));

    // ── Loading ──────────────────────────────────────────────────────────

    private void LoadAll()
    {
        var previousId = SelectedWorkflow?.Id;
        Workflows.Clear();

        var items = _store.ListAsync(App.SovrantUserId, status: null, limit: 200).GetAwaiter().GetResult();
        foreach (var w in items.OrderByDescending(w => w.CreatedAt))
        {
            Workflows.Add(new WorkflowItemViewModel
            {
                Id = w.Id,
                Goal = w.Goal,
                Status = w.Status,
                Subtitle = w.CompletedAt is not null
                    ? $"finished {w.CompletedAt.Value.ToLocalTime():MMM d, HH:mm}"
                    : $"started {w.CreatedAt.ToLocalTime():MMM d, HH:mm}",
            });
        }

        WorkflowCount = Workflows.Count;
        if (previousId is not null)
            SelectedWorkflow = Workflows.FirstOrDefault(w => w.Id == previousId);
    }

    private async Task LoadDetailAsync(WorkflowItemViewModel item)
    {
        item.Steps.Clear();
        item.Events.Clear();

        var plan = ParsePlan(item.PlanJson);
        if (plan is not null)
            foreach (var s in plan.Steps)
                item.Steps.Add(new PlanStepViewModel { Index = s.Index, Intent = s.Intent, Expected = s.Expected, Tier = s.Tier });

        var events = await _store.GetEventsAsync(item.Id).ConfigureAwait(true);
        foreach (var e in events)
            item.Events.Add(new WorkflowEventItemViewModel
            {
                Timestamp = e.Timestamp.ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                EventType = e.EventType,
            });
    }

    private static PlanSnapshot? ParsePlan(string planJson)
    {
        if (string.IsNullOrWhiteSpace(planJson) || planJson == "{}") return null;
        try
        {
            return JsonSerializer.Deserialize<PlanSnapshot>(planJson, s_jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsTerminal(WorkflowStatus status) =>
        status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.Cancelled;

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via JsonSerializer.Deserialize<T>, which the analyzer can't trace.")]
    private sealed class PlanSnapshot
    {
        public List<PlanStepSnapshot> Steps { get; set; } = [];
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via JsonSerializer.Deserialize<T>, which the analyzer can't trace.")]
    private sealed class PlanStepSnapshot
    {
        public int Index { get; set; }
        public string Intent { get; set; } = string.Empty;
        public string Expected { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
    }
}

public partial class WorkflowItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _goal = string.Empty;
    [ObservableProperty] private WorkflowStatus _status;
    [ObservableProperty] private string _subtitle = string.Empty;
    [ObservableProperty] private string _planJson = "{}";

    public string StatusName => Status.ToString();
    public bool IsTerminal => Status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.Cancelled;

    public ObservableCollection<PlanStepViewModel> Steps { get; } = [];
    public ObservableCollection<WorkflowEventItemViewModel> Events { get; } = [];
}

public partial class PlanStepViewModel : ViewModelBase
{
    [ObservableProperty] private int _index;
    [ObservableProperty] private string _intent = string.Empty;
    [ObservableProperty] private string _expected = string.Empty;
    [ObservableProperty] private string _tier = string.Empty;
}

public partial class WorkflowEventItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _timestamp = string.Empty;
    [ObservableProperty] private string _eventType = string.Empty;
}
