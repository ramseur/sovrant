using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Agents.Models;
using Sovrant.Agents.Orchestration;
using Sovrant.Agents.Swarm;
using Sovrant.Agents.Teams;
using Sovrant.Agents.Templates;

namespace Sovrant.Desktop.ViewModels;

public partial class OrchestrationViewModel : ViewModelBase
{
    private readonly ITeamRegistry _teamRegistry;
    private readonly ISwarmConfigStore _swarmConfigStore;
    private readonly IAgentOrchestrator _orchestrator;
    private readonly AgentTemplateRegistry _templateRegistry;
    private readonly ActiveContextViewModel _activeContext;

    [ObservableProperty] private int _teamCount;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private TeamItemViewModel? _selectedTeam;
    [ObservableProperty] private bool _showSwarmConfig;

    // ── Create team ────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isCreating;
    [ObservableProperty] private string _createName = string.Empty;
    [ObservableProperty] private string _createDescription = string.Empty;
    [ObservableProperty] private string _createRunModeName = nameof(TeamRunMode.Parallel);

    // ── Add member ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isAddingMember;
    [ObservableProperty] private string _addMemberTemplate = string.Empty;
    [ObservableProperty] private string _addMemberName = string.Empty;
    [ObservableProperty] private string _addMemberRole = nameof(AgentRole.General);

    // ── Run ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _runGoal = string.Empty;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _runStatus = string.Empty;
    [ObservableProperty] private string _runOutput = string.Empty;

    public bool HasSelection => SelectedTeam is not null && !ShowSwarmConfig && !IsCreating;
    public bool HasNoSelection => SelectedTeam is null && !ShowSwarmConfig && !IsCreating;

    public ObservableCollection<TeamItemViewModel> Teams { get; } = [];
    public ObservableCollection<AgentTemplateOptionViewModel> TemplateOptions { get; } = [];

    public IReadOnlyList<string> RunModeOptions { get; } =
        [nameof(TeamRunMode.Sequential), nameof(TeamRunMode.Parallel), nameof(TeamRunMode.Swarm)];

    public IReadOnlyList<string> DecompositionModeOptions { get; } =
        [nameof(TeamDecompositionMode.Off), nameof(TeamDecompositionMode.RoleAware), nameof(TeamDecompositionMode.Open)];

    public IReadOnlyList<string> RoleOptions { get; } = Enum.GetNames<AgentRole>();

    [ObservableProperty] private bool _swarmEnabled;
    [ObservableProperty] private int _maxConcurrent;
    [ObservableProperty] private int _maxTokenBudget;
    [ObservableProperty] private int _maxRetries;
    [ObservableProperty] private bool _qualityGateEnabled;
    [ObservableProperty] private string _swarmPermissions = "ask";
    [ObservableProperty] private string _decomposerLevel = "High";
    [ObservableProperty] private string _workerLevel = "Standard";
    [ObservableProperty] private int _taskTimeoutSeconds = 300;

    public OrchestrationViewModel(
        ITeamRegistry teamRegistry,
        ISwarmConfigStore swarmConfigStore,
        IAgentOrchestrator orchestrator,
        AgentTemplateRegistry templateRegistry,
        ActiveContextViewModel activeContext)
    {
        _teamRegistry = teamRegistry;
        _swarmConfigStore = swarmConfigStore;
        _orchestrator = orchestrator;
        _templateRegistry = templateRegistry;
        _activeContext = activeContext;
        LoadSwarmConfig();
        LoadTemplateOptions();
        LoadAll();
    }

    // ── Commands ───────────────────────────────────────────────────────────

    [RelayCommand]
    private void Refresh()
    {
        LoadSwarmConfig();
        LoadAll();
    }

    [RelayCommand]
    private void SelectTeam(TeamItemViewModel team)
    {
        ShowSwarmConfig = false;
        IsCreating = false;
        IsAddingMember = false;
        RunGoal = string.Empty;
        RunStatus = string.Empty;
        RunOutput = string.Empty;
        SelectedTeam = team;
    }

    [RelayCommand]
    private void ShowTeamView()
    {
        ShowSwarmConfig = false;
        IsCreating = false;
    }

    [RelayCommand]
    private void ShowDefaultsView()
    {
        ShowSwarmConfig = true;
        SelectedTeam = null;
        IsCreating = false;
    }

    // ── Create team ────────────────────────────────────────────────────────

    [RelayCommand]
    private void StartCreate()
    {
        IsCreating = true;
        ShowSwarmConfig = false;
        CreateName = string.Empty;
        CreateDescription = string.Empty;
        CreateRunModeName = nameof(TeamRunMode.Parallel);
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void CancelCreate() => IsCreating = false;

    [RelayCommand]
    private void ConfirmCreate()
    {
        var name = CreateName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        if (!Enum.TryParse<TeamRunMode>(CreateRunModeName, out var runMode)) runMode = TeamRunMode.Parallel;

        var team = new TeamInfo(
            Id: $"team-{Guid.NewGuid():N}",
            WorkspaceId: _activeContext.ActiveWorkspaceId ?? string.Empty,
            ProjectId: string.IsNullOrEmpty(_activeContext.ActiveProjectId) ? null : _activeContext.ActiveProjectId,
            Name: name,
            Description: string.IsNullOrEmpty(CreateDescription) ? null : CreateDescription.Trim(),
            Origin: "ui",
            CreatedBy: App.SovrantUserId,
            CreatedAt: DateTimeOffset.UtcNow)
        {
            RunMode = runMode,
        };

        _teamRegistry.CreateTeam(team);
        IsCreating = false;
        StatusMessage = $"Created team '{name}'.";
        LoadAll();
        SelectedTeam = Teams.FirstOrDefault(t => t.TeamId == team.Id);
    }

    // ── Add member ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void StartAddMember()
    {
        IsAddingMember = true;
        AddMemberTemplate = string.Empty;
        AddMemberName = string.Empty;
        AddMemberRole = nameof(AgentRole.General);
    }

    [RelayCommand]
    private void CancelAddMember() => IsAddingMember = false;

    [RelayCommand]
    private void SelectMemberTemplate(string templateName)
    {
        AddMemberTemplate = templateName;
        var t = _templateRegistry.TryGet(templateName);
        if (t is null) return;
        AddMemberName = t.Name;
        AddMemberRole = t.Role.ToString();
    }

    [RelayCommand]
    private void ConfirmAddMember()
    {
        if (SelectedTeam is null || string.IsNullOrWhiteSpace(AddMemberName)) return;

        var template = string.IsNullOrEmpty(AddMemberTemplate) ? null : _templateRegistry.TryGet(AddMemberTemplate);
        if (!Enum.TryParse<AgentRole>(AddMemberRole, out var role)) role = AgentRole.General;

        var member = new TeamMemberInfo
        {
            Id = $"member-{Guid.NewGuid():N}",
            Name = AddMemberName.Trim(),
            Role = role,
            SystemPrompt = template?.SystemPrompt ?? $"You are a {AddMemberRole} agent.",
            AllowedTools = template?.AllowedTools,
            TeamId = SelectedTeam.TeamId,
            WorkspaceId = _activeContext.ActiveWorkspaceId ?? string.Empty,
            Template = string.IsNullOrEmpty(AddMemberTemplate) ? null : AddMemberTemplate,
            CreatedBy = App.SovrantUserId,
        };

        _teamRegistry.RegisterMember(member);
        IsAddingMember = false;
        StatusMessage = $"Added '{member.Name}' to team.";
        LoadAll();
    }

    // ── Run ────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanRunTeam))]
    private async Task RunTeamAsync()
    {
        if (SelectedTeam is null || string.IsNullOrWhiteSpace(RunGoal) || IsRunning) return;

        IsRunning = true;
        RunStatus = "Starting…";
        RunOutput = string.Empty;

        try
        {
            var request = new EnsembleRunRequest
            {
                Goal = RunGoal,
                TeamId = SelectedTeam.TeamId,
                WorkspaceId = _activeContext.ActiveWorkspaceId ?? string.Empty,
                UserId = App.SovrantUserId,
                Permissions = "ask",
            };

            var result = await _orchestrator.RunAsync(request).ConfigureAwait(false);
            var sr = result.SwarmResult;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RunStatus = sr.Status.ToString();
                RunOutput = string.IsNullOrEmpty(sr.CombinedOutput) ? "(no output)" : sr.CombinedOutput;
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RunStatus = "Failed";
                RunOutput = ex.Message;
            });
        }
        finally
        {
            IsRunning = false;
        }
    }

    private bool CanRunTeam() => SelectedTeam is not null && !string.IsNullOrWhiteSpace(RunGoal) && !IsRunning;

    // ── Team profile / swarm / other ───────────────────────────────────────

    [RelayCommand]
    private void RemoveTeam(TeamItemViewModel team)
    {
        _teamRegistry.RemoveTeam(team.TeamId);
        StatusMessage = $"Removed team '{team.Name}'.";
        SelectedTeam = null;
        LoadAll();
    }

    [RelayCommand]
    private void RemoveMember(TeamMemberItemViewModel member)
    {
        _teamRegistry.RemoveMember(member.Id);
        StatusMessage = $"Removed member '{member.Name}'.";
        LoadAll();
    }

    [RelayCommand]
    private void SaveTeamProfile(TeamItemViewModel team)
    {
        if (!Enum.TryParse<TeamRunMode>(team.RunModeName, ignoreCase: true, out var runMode))
        {
            StatusMessage = $"Unknown run mode '{team.RunModeName}'.";
            return;
        }

        if (!Enum.TryParse<TeamDecompositionMode>(team.DecompositionModeName, ignoreCase: true, out var decompositionMode))
        {
            StatusMessage = $"Unknown decomposition mode '{team.DecompositionModeName}'.";
            return;
        }

        var profile = new TeamRunProfile(
            runMode, team.MaxConcurrent, team.FileLocksEnabled,
            team.QualityGateEnabled, team.QualityGateThreshold, decompositionMode);

        if (_teamRegistry.UpdateTeamRunProfile(team.TeamId, profile))
        {
            StatusMessage = $"Saved run profile for '{team.Name}'.";
            LoadAll();
        }
        else
        {
            StatusMessage = $"Could not save '{team.Name}' — team not found.";
        }
    }

    partial void OnSelectedTeamChanged(TeamItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSelection));
        RunTeamCommand.NotifyCanExecuteChanged();
    }

    partial void OnShowSwarmConfigChanged(bool value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSelection));
    }

    partial void OnIsCreatingChanged(bool value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSelection));
    }

    partial void OnRunGoalChanged(string value) => RunTeamCommand.NotifyCanExecuteChanged();

    private void LoadAll()
    {
        var previousTeamId = SelectedTeam?.TeamId;
        Teams.Clear();

        foreach (var t in _teamRegistry.ListTeams())
        {
            var members = _teamRegistry.GetTeamMembers(t.Id).ToList();
            var item = new TeamItemViewModel
            {
                TeamId = t.Id,
                Name = t.Name,
                Description = t.Description ?? string.Empty,
                Subtitle = members.Count == 1 ? "1 agent" : $"{members.Count} agents",
                RunModeName = t.RunMode.ToString(),
                ModeLabel = t.RunMode.ToString(),
                MaxConcurrent = t.MaxConcurrent,
                FileLocksEnabled = t.FileLocksEnabled,
                QualityGateEnabled = t.QualityGateEnabled,
                QualityGateThreshold = t.QualityGateThreshold,
                DecompositionModeName = t.DecompositionMode.ToString(),
            };
            foreach (var m in members) item.Members.Add(ToViewModel(m));
            Teams.Add(item);
        }

        TeamCount = Teams.Count;
        if (previousTeamId is not null)
            SelectedTeam = Teams.FirstOrDefault(x => x.TeamId == previousTeamId);
    }

    private void LoadTemplateOptions()
    {
        TemplateOptions.Clear();
        TemplateOptions.Add(new AgentTemplateOptionViewModel { Name = string.Empty, Label = "— custom —" });
        foreach (var t in _templateRegistry.All)
            TemplateOptions.Add(new AgentTemplateOptionViewModel { Name = t.Name, Label = t.Name });
    }

    private static TeamMemberItemViewModel ToViewModel(TeamMemberInfo m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Role = m.Role.ToString(),
        Model = m.Model ?? string.Empty,
        ToolsSummary = BuildToolsSummary(m.AllowedTools),
    };

    private static string BuildToolsSummary(IReadOnlyList<string>? tools)
    {
        if (tools is null || tools.Count == 0) return "All tools";
        if (tools.Count <= 3) return string.Join(", ", tools);
        return $"{string.Join(", ", tools.Take(3))} +{tools.Count - 3} more";
    }

    [RelayCommand]
    private async Task ToggleSwarmAsync()
    {
        SwarmEnabled = !SwarmEnabled;
        await SaveSwarmConfigAsync().ConfigureAwait(true);
        LoadAll();
    }

    [RelayCommand]
    private async Task ToggleQualityGateAsync()
    {
        QualityGateEnabled = !QualityGateEnabled;
        await SaveSwarmConfigAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveSwarmAsync()
    {
        await SaveSwarmConfigAsync().ConfigureAwait(true);
        StatusMessage = "Swarm defaults saved.";
        LoadAll();
    }

    private void LoadSwarmConfig()
    {
        var config = _swarmConfigStore.GetAsync().GetAwaiter().GetResult();
        SwarmEnabled = config.Enabled;
        MaxConcurrent = config.MaxConcurrent;
        MaxTokenBudget = config.MaxTokenBudget;
        MaxRetries = config.MaxRetries;
        QualityGateEnabled = config.QualityGateEnabled;
        SwarmPermissions = config.Permissions;
        DecomposerLevel = config.DecomposerLevel;
        WorkerLevel = config.WorkerLevel;
        TaskTimeoutSeconds = config.TaskTimeoutSeconds;
    }

    private async Task SaveSwarmConfigAsync()
    {
        try
        {
            var config = new SwarmConfig
            {
                Enabled              = SwarmEnabled,
                MaxConcurrent        = MaxConcurrent,
                MaxTokenBudget       = MaxTokenBudget,
                MaxRetries           = MaxRetries,
                QualityGateEnabled   = QualityGateEnabled,
                Permissions          = SwarmPermissions,
                DecomposerLevel      = DecomposerLevel,
                WorkerLevel          = WorkerLevel,
                TaskTimeoutSeconds   = TaskTimeoutSeconds,
            };
            await _swarmConfigStore.SetAsync(string.Empty, config).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }
}

public partial class TeamItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _teamId = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _subtitle = string.Empty;
    [ObservableProperty] private string _runModeName = nameof(TeamRunMode.Parallel);
    [ObservableProperty] private string _modeLabel = string.Empty;
    [ObservableProperty] private int _maxConcurrent = 4;
    [ObservableProperty] private bool _fileLocksEnabled;
    [ObservableProperty] private bool _qualityGateEnabled;
    [ObservableProperty] private int _qualityGateThreshold = 7;
    [ObservableProperty] private string _decompositionModeName = nameof(TeamDecompositionMode.Off);

    public ObservableCollection<TeamMemberItemViewModel> Members { get; } = [];
}

public partial class TeamMemberItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _role = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _toolsSummary = string.Empty;
}

public sealed class AgentTemplateOptionViewModel
{
    public string Name { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}
