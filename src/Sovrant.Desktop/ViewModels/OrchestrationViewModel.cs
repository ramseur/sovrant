using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Agents.Swarm;
using Sovrant.Agents.Teams;

namespace Sovrant.Desktop.ViewModels;

public partial class OrchestrationViewModel : ViewModelBase
{
    private readonly ITeamRegistry _teamRegistry;
    private readonly ISwarmConfigStore _swarmConfigStore;

    [ObservableProperty] private int _teamCount;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private TeamItemViewModel? _selectedTeam;
    [ObservableProperty] private bool _showSwarmConfig;

    public bool HasSelection => SelectedTeam is not null && !ShowSwarmConfig;
    public bool HasNoSelection => SelectedTeam is null && !ShowSwarmConfig;

    public ObservableCollection<TeamItemViewModel> Teams { get; } = [];

    public IReadOnlyList<string> RunModeOptions { get; } =
        [nameof(TeamRunMode.Sequential), nameof(TeamRunMode.Parallel), nameof(TeamRunMode.Swarm)];

    public IReadOnlyList<string> DecompositionModeOptions { get; } =
        [nameof(TeamDecompositionMode.Off), nameof(TeamDecompositionMode.RoleAware), nameof(TeamDecompositionMode.Open)];

    [ObservableProperty] private bool _swarmEnabled;
    [ObservableProperty] private int _maxConcurrent;
    [ObservableProperty] private int _maxTokenBudget;
    [ObservableProperty] private int _maxRetries;
    [ObservableProperty] private bool _qualityGateEnabled;
    [ObservableProperty] private string _swarmPermissions = "ask";
    [ObservableProperty] private string _decomposerLevel = "High";
    [ObservableProperty] private string _workerLevel = "Standard";
    [ObservableProperty] private int _taskTimeoutSeconds = 300;

    public OrchestrationViewModel(ITeamRegistry teamRegistry, ISwarmConfigStore swarmConfigStore)
    {
        _teamRegistry = teamRegistry;
        _swarmConfigStore = swarmConfigStore;
        LoadSwarmConfig();
        LoadAll();
    }

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
        SelectedTeam = team;
    }

    [RelayCommand]
    private void ToggleSwarmConfig()
    {
        ShowSwarmConfig = !ShowSwarmConfig;
        if (ShowSwarmConfig) SelectedTeam = null;
    }

    [RelayCommand]
    private void HintNew()
    {
        StatusMessage = "New team picker coming soon. Use /team create in chat to add a team.";
    }

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
            runMode,
            team.MaxConcurrent,
            team.FileLocksEnabled,
            team.QualityGateEnabled,
            team.QualityGateThreshold,
            decompositionMode);

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
    }

    partial void OnShowSwarmConfigChanged(bool value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSelection));
    }

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

            foreach (var m in members)
                item.Members.Add(ToViewModel(m));

            Teams.Add(item);
        }

        TeamCount = Teams.Count;

        if (previousTeamId is not null)
            SelectedTeam = Teams.FirstOrDefault(x => x.TeamId == previousTeamId);
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
