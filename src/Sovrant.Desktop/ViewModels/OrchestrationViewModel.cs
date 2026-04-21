using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Agents.Swarm;
using Sovrant.Agents.Teams;

namespace Sovrant.Desktop.ViewModels;

public enum TeamRunMode { Solo, Sequential, Swarm }

public partial class OrchestrationViewModel : ViewModelBase
{
    private readonly ITeamRegistry _teamRegistry;

    [ObservableProperty] private int _teamCount;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private TeamItemViewModel? _selectedTeam;
    [ObservableProperty] private bool _showSwarmConfig;

    public bool HasSelection => SelectedTeam is not null && !ShowSwarmConfig;
    public bool HasNoSelection => SelectedTeam is null && !ShowSwarmConfig;

    public ObservableCollection<TeamItemViewModel> Teams { get; } = [];

    [ObservableProperty] private bool _swarmEnabled;
    [ObservableProperty] private int _maxConcurrent;
    [ObservableProperty] private int _maxTokenBudget;
    [ObservableProperty] private int _maxRetries;
    [ObservableProperty] private bool _qualityGateEnabled;
    [ObservableProperty] private string _swarmPermissions = "ask";
    [ObservableProperty] private string _decomposerLevel = "High";
    [ObservableProperty] private string _workerLevel = "Standard";
    [ObservableProperty] private int _taskTimeoutSeconds = 300;

    private static readonly string SwarmConfigPath = Path.Combine(
        Directory.GetCurrentDirectory(), ".sovrant", "swarm.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public OrchestrationViewModel(ITeamRegistry teamRegistry)
    {
        _teamRegistry = teamRegistry;
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
            var mode = members.Count switch
            {
                <= 1 => TeamRunMode.Solo,
                _ => SwarmEnabled ? TeamRunMode.Swarm : TeamRunMode.Sequential,
            };

            var item = new TeamItemViewModel
            {
                TeamId = t.Id,
                Name = t.Name,
                Description = t.Description ?? string.Empty,
                Subtitle = members.Count == 1 ? "1 agent" : $"{members.Count} agents",
                Mode = mode,
                ModeLabel = mode.ToString(),
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
    private void ToggleSwarm()
    {
        SwarmEnabled = !SwarmEnabled;
        SaveSwarmConfig();
        LoadAll();
    }

    [RelayCommand]
    private void ToggleQualityGate()
    {
        QualityGateEnabled = !QualityGateEnabled;
        SaveSwarmConfig();
    }

    [RelayCommand]
    private void SaveSwarm()
    {
        SaveSwarmConfig();
        StatusMessage = "Swarm defaults saved.";
        LoadAll();
    }

    private void LoadSwarmConfig()
    {
        var config = SwarmConfigLoader.Load();
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

    private void SaveSwarmConfig()
    {
        try
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["enabled"] = SwarmEnabled,
                ["max_concurrent"] = MaxConcurrent,
                ["max_token_budget"] = MaxTokenBudget,
                ["max_retries"] = MaxRetries,
                ["quality_gate"] = QualityGateEnabled,
                ["permissions"] = SwarmPermissions,
                ["decomposer_level"] = DecomposerLevel,
                ["worker_level"] = WorkerLevel,
                ["task_timeout_seconds"] = TaskTimeoutSeconds,
            };

            var dir = Path.GetDirectoryName(SwarmConfigPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(dict, SerializerOptions);
            File.WriteAllText(SwarmConfigPath, json);
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
    [ObservableProperty] private TeamRunMode _mode;
    [ObservableProperty] private string _modeLabel = string.Empty;

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
