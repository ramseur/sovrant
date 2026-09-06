using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Workflows;

namespace Sovrant.Desktop.ViewModels;

public partial class WorkflowsViewModel : ViewModelBase
{
    public IReadOnlyList<string> TierOptions { get; } = ["Standard", "High", "Fast"];

    private readonly IWorkflowStore _store;
    private readonly IWorkflowExecutor _executor;
    private readonly WorkflowPlanningService _planner;
    private readonly WorkflowExportService _exporter;
    private readonly ActiveContextViewModel _activeContext;

    [ObservableProperty] private int _workflowCount;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private WorkflowItemViewModel? _selectedWorkflow;

    [ObservableProperty] private bool _isCreating;
    [ObservableProperty] private string _createGoal = string.Empty;
    [ObservableProperty] private bool _isGeneratingPlan;
    [ObservableProperty] private bool _isSavingPlan;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _showExport;
    [ObservableProperty] private string _exportText = string.Empty;

    public bool HasSelection => SelectedWorkflow is not null && !IsCreating;
    public bool HasNoSelection => SelectedWorkflow is null && !IsCreating;
    public string RunButtonLabel =>
        IsRunning ? "Running…" : SelectedWorkflow?.Status == WorkflowStatus.AwaitingHuman ? "Resume" : "Run now";

    /// <summary>
    /// True when the selected workflow has a plan waiting for human review
    /// before it has ever run — as opposed to AwaitingHuman from a post-run
    /// acceptance-gate pause, which shows the plan read-only like normal.
    /// </summary>
    public bool IsPlanReview =>
        SelectedWorkflow is { Status: WorkflowStatus.AwaitingHuman, HasRunStarted: false };

    public ObservableCollection<WorkflowItemViewModel> Workflows { get; } = [];

    public WorkflowsViewModel(
        IWorkflowStore store,
        IWorkflowExecutor executor,
        WorkflowPlanningService planner,
        WorkflowExportService exporter,
        ActiveContextViewModel activeContext)
    {
        _store = store;
        _executor = executor;
        _planner = planner;
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

    [RelayCommand]
    private async Task GeneratePlanAsync()
    {
        var goal = CreateGoal.Trim();
        if (string.IsNullOrEmpty(goal) || IsGeneratingPlan) return;

        IsGeneratingPlan = true;
        try
        {
            var workflow = await _planner.GenerateAsync(
                goal,
                workspaceId: _activeContext.ActiveWorkspaceId,
                projectId: string.IsNullOrEmpty(_activeContext.ActiveProjectId) ? null : _activeContext.ActiveProjectId,
                ownerUserId: App.SovrantUserId).ConfigureAwait(true);

            IsCreating = false;
            StatusMessage = "Plan generated — review the steps below, then Save or Resume to run.";
            LoadAll();
            SelectedWorkflow = Workflows.FirstOrDefault(w => w.Id == workflow.Id);
            if (SelectedWorkflow is not null)
                await LoadDetailAsync(SelectedWorkflow).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => StatusMessage = $"Plan generation failed: {ex.Message}");
        }
        finally
        {
            IsGeneratingPlan = false;
        }
    }

    // ── Plan review ──────────────────────────────────────────────────────

    [RelayCommand]
    private void AddStep()
    {
        if (SelectedWorkflow is null) return;
        SelectedWorkflow.Steps.Add(new PlanStepViewModel { Index = SelectedWorkflow.Steps.Count, Tier = "Standard" });
    }

    [RelayCommand]
    private void RemoveStep(PlanStepViewModel step)
    {
        if (SelectedWorkflow is null || SelectedWorkflow.Steps.Count <= 1) return;
        SelectedWorkflow.Steps.Remove(step);
    }

    [RelayCommand]
    private async Task SavePlanAsync()
    {
        if (SelectedWorkflow is null || IsSavingPlan) return;

        var steps = SelectedWorkflow.Steps
            .Where(s => !string.IsNullOrWhiteSpace(s.Intent))
            .Select((s, i) => new RuntimeStep(
                i, s.Intent.Trim(),
                string.IsNullOrWhiteSpace(s.Expected) ? "step completed successfully" : s.Expected.Trim(),
                ParseTier(s.Tier)))
            .ToList();
        if (steps.Count == 0)
        {
            StatusMessage = "Add at least one step with an intent before saving.";
            return;
        }

        IsSavingPlan = true;
        try
        {
            await _planner.SavePlanAsync(SelectedWorkflow.Id, steps).ConfigureAwait(true);
            StatusMessage = "Plan saved.";
            LoadAll();
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => StatusMessage = $"Save failed: {ex.Message}");
        }
        finally
        {
            IsSavingPlan = false;
        }
    }

    private static RuntimeModelTier ParseTier(string tier) => tier switch
    {
        "High" => RuntimeModelTier.High,
        "Fast" => RuntimeModelTier.Fast,
        _ => RuntimeModelTier.Standard,
    };

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
        OnPropertyChanged(nameof(IsPlanReview));
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

        var plan = WorkflowPlanJson.TryDeserialize(item.PlanJson, item.Goal);
        if (plan is not null)
            foreach (var s in plan.Steps)
                item.Steps.Add(new PlanStepViewModel { Index = s.Index, Intent = s.Intent, Expected = s.ExpectedOutcome, Tier = s.ModelTier.ToString() });

        var events = await _store.GetEventsAsync(item.Id).ConfigureAwait(true);
        item.HasRunStarted = events.Any(e => e.EventType == WorkflowEventTypes.RunStarted);
        foreach (var e in events)
            item.Events.Add(new WorkflowEventItemViewModel
            {
                Timestamp = e.Timestamp.ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                EventType = HumanizeEventType(e.EventType),
            });

        if (ReferenceEquals(SelectedWorkflow, item))
            OnPropertyChanged(nameof(IsPlanReview));
    }

    // The event_type values persisted in the journal (mission_created, etc.)
    // are historical data left unrenamed on purpose -- rewriting rows for a
    // cosmetic win wasn't worth the risk. This only humanizes the display.
    private static string HumanizeEventType(string eventType) => eventType switch
    {
        WorkflowEventTypes.WorkflowCreated => "Workflow created",
        WorkflowEventTypes.PlanRevised => "Plan revised",
        WorkflowEventTypes.RunStarted => "Run started",
        WorkflowEventTypes.RunCompleted => "Run completed",
        WorkflowEventTypes.AcceptanceApproved => "Accepted",
        WorkflowEventTypes.AcceptanceRejected => "Rejected",
        WorkflowEventTypes.Paused => "Paused",
        WorkflowEventTypes.Resumed => "Resumed",
        WorkflowEventTypes.Completed => "Completed",
        WorkflowEventTypes.Failed => "Failed",
        WorkflowEventTypes.Cancelled => "Cancelled",
        _ => eventType.Replace('_', ' '),
    };

    private static bool IsTerminal(WorkflowStatus status) =>
        status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.Cancelled;
}

public partial class WorkflowItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _goal = string.Empty;
    [ObservableProperty] private WorkflowStatus _status;
    [ObservableProperty] private string _subtitle = string.Empty;
    [ObservableProperty] private string _planJson = "{}";

    /// <summary>Whether a RunStarted event has ever been journaled for this workflow — distinguishes a pre-run plan review from a post-run acceptance pause, both of which report AwaitingHuman.</summary>
    [ObservableProperty] private bool _hasRunStarted;

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
