using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Agents.Swarm;
using Sovrant.Agents.Teams;

namespace Sovrant.Desktop.ViewModels;

public partial class MultiAgentViewModel : ViewModelBase
{
    private readonly ITeamRegistry _teamRegistry;

    // --- Header ---
    [ObservableProperty] private int _teamCount;
    [ObservableProperty] private int _memberCount;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // --- Tab selection ---
    [ObservableProperty] private int _selectedTab; // 0=Teams, 1=Swarm, 2=Claws
    public bool IsTeamsTab => SelectedTab == 0;
    public bool IsSwarmTab => SelectedTab == 1;
    public bool IsClawsTab => SelectedTab == 2;

    // --- Teams tab ---
    [ObservableProperty] private TeamMemberItemViewModel? _selectedMember;
    [ObservableProperty] private string _detailMarkdown = string.Empty;

    public ObservableCollection<TeamGroupViewModel> Teams { get; } = [];

    // --- Swarm tab ---
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

    public MultiAgentViewModel(ITeamRegistry teamRegistry)
    {
        _teamRegistry = teamRegistry;
        LoadAll();
        LoadSwarmConfig();
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadAll();
        LoadSwarmConfig();
    }

    [RelayCommand]
    private void SelectTab(string tab)
    {
        if (int.TryParse(tab, out var index))
            SelectedTab = index;
    }

    // ─── Teams ────────────────────────────────────────

    [RelayCommand]
    private void SelectMember(TeamMemberItemViewModel member) => SelectedMember = member;

    [RelayCommand]
    private void RemoveMember(TeamMemberItemViewModel member)
    {
        _teamRegistry.RemoveMember(member.Id);
        StatusMessage = $"Removed member '{member.Name}'.";
        LoadAll();
        if (SelectedMember == member) SelectedMember = null;
    }

    [RelayCommand]
    private void RemoveTeam(TeamGroupViewModel team)
    {
        _teamRegistry.RemoveTeam(team.TeamId);
        StatusMessage = $"Removed team '{team.TeamName}'.";
        LoadAll();
        SelectedMember = null;
    }

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsTeamsTab));
        OnPropertyChanged(nameof(IsSwarmTab));
        OnPropertyChanged(nameof(IsClawsTab));
    }

    partial void OnSelectedMemberChanged(TeamMemberItemViewModel? value)
    {
        DetailMarkdown = value is null ? string.Empty : BuildMemberMarkdown(value);
    }

    private void LoadAll()
    {
        Teams.Clear();

        var teams = _teamRegistry.ListTeams();
        var allMembers = _teamRegistry.GetAllMembers();

        // Group members by team
        var teamGroups = new Dictionary<string, List<TeamMemberInfo>>(StringComparer.Ordinal);
        var unassigned = new List<TeamMemberInfo>();

        foreach (var m in allMembers)
        {
            if (!string.IsNullOrEmpty(m.TeamId))
            {
                if (!teamGroups.TryGetValue(m.TeamId, out var list))
                {
                    list = [];
                    teamGroups[m.TeamId] = list;
                }
                list.Add(m);
            }
            else
            {
                unassigned.Add(m);
            }
        }

        // Add team groups
        foreach (var team in teams)
        {
            var members = teamGroups.TryGetValue(team.Id, out var list) ? list : [];
            var group = new TeamGroupViewModel
            {
                TeamId = team.Id,
                TeamName = team.Name,
                TeamDescription = team.Description ?? string.Empty,
            };

            foreach (var m in members)
                group.Members.Add(ToViewModel(m));

            Teams.Add(group);
        }

        // Add unassigned members
        if (unassigned.Count > 0)
        {
            var group = new TeamGroupViewModel
            {
                TeamId = string.Empty,
                TeamName = "Unassigned Members",
                TeamDescription = "Members not assigned to any team",
            };

            foreach (var m in unassigned)
                group.Members.Add(ToViewModel(m));

            Teams.Add(group);
        }

        TeamCount = teams.Count;
        MemberCount = allMembers.Count;
    }

    private static TeamMemberItemViewModel ToViewModel(TeamMemberInfo m)
    {
        var item = new TeamMemberItemViewModel
        {
            Id = m.Id,
            Name = m.Name,
            Role = m.Role.ToString(),
            TemplateName = m.Template ?? string.Empty,
            Model = m.Model ?? string.Empty,
            SystemPrompt = m.SystemPrompt,
            ToolCount = m.AllowedTools?.Count ?? 0,
            AllowedTools = m.AllowedTools ?? [],
            CreatedAt = m.CreatedAt,
        };
        item.Markdown = BuildMemberMarkdown(item);
        return item;
    }

    private static string BuildMemberMarkdown(TeamMemberItemViewModel member)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {member.Name}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Role:** {member.Role}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**ID:** {member.Id}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(member.TemplateName))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Template:** {member.TemplateName}");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(member.Model))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Model:** {member.Model}");
            sb.AppendLine();
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"**Created:** {member.CreatedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        if (member.AllowedTools.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Tools ({member.AllowedTools.Count}):** {string.Join(", ", member.AllowedTools)}");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(member.SystemPrompt))
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## System Prompt");
            sb.AppendLine();
            sb.Append(AgentsViewModel.SanitizeForMarkdown(member.SystemPrompt));
        }

        return sb.ToString();
    }

    // ─── Swarm Config ─────────────────────────────────

    [RelayCommand]
    private void ToggleSwarm()
    {
        SwarmEnabled = !SwarmEnabled;
        SaveSwarmConfig();
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
        StatusMessage = "Swarm configuration saved.";
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

public partial class TeamGroupViewModel : ViewModelBase
{
    [ObservableProperty] private string _teamId = string.Empty;
    [ObservableProperty] private string _teamName = string.Empty;
    [ObservableProperty] private string _teamDescription = string.Empty;

    public ObservableCollection<TeamMemberItemViewModel> Members { get; } = [];
}

public partial class TeamMemberItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _role = string.Empty;
    [ObservableProperty] private string _templateName = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private int _toolCount;
    [ObservableProperty] private DateTimeOffset _createdAt;

    public string SystemPrompt { get; init; } = string.Empty;
    public IReadOnlyList<string> AllowedTools { get; init; } = [];
    public string Markdown { get; set; } = string.Empty;
}
