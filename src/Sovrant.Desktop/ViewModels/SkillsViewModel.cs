using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Tools.Skills;

namespace Sovrant.Desktop.ViewModels;

public partial class SkillsViewModel : ViewModelBase
{
    private readonly SkillRegistry _registry;
    private readonly List<SkillItemViewModel> _allSkills = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private SkillItemViewModel? _selectedSkill;

    [ObservableProperty]
    private string _detailMarkdown = string.Empty;

    public ObservableCollection<SkillItemViewModel> FilteredSkills { get; } = [];

    public SkillsViewModel(SkillRegistry registry)
    {
        _registry = registry;
        LoadSkills();
    }

    [RelayCommand]
    private void Refresh() => LoadSkills();

    [RelayCommand]
    private void SelectSkill(SkillItemViewModel skill) => SelectedSkill = skill;

    private void LoadSkills()
    {
        _allSkills.Clear();
        foreach (var s in _registry.All.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            var item = new SkillItemViewModel
            {
                Name = s.Name,
                Description = s.Description,
                Trigger = string.IsNullOrEmpty(s.Trigger) ? "(none)" : s.Trigger,
                AgentCount = s.Agents.Count,
                ToolCount = s.Tools.Count,
                Agents = s.Agents,
                Tools = s.Tools,
                Body = s.Body,
            };
            item.Markdown = BuildSkillMarkdown(item);
            _allSkills.Add(item);
        }

        TotalCount = _allSkills.Count;
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedSkillChanged(SkillItemViewModel? value)
    {
        DetailMarkdown = value is null ? string.Empty : BuildSkillMarkdown(value);
    }

    private void ApplyFilter()
    {
        FilteredSkills.Clear();
        var query = SearchText.Trim();

        foreach (var skill in _allSkills)
        {
            if (query.Length > 0
                && !skill.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !skill.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FilteredSkills.Add(skill);
        }
    }

    private static string BuildSkillMarkdown(SkillItemViewModel skill)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {skill.Name}");
        sb.AppendLine();
        sb.AppendLine(skill.Description);
        sb.AppendLine();

        if (skill.Trigger != "(none)")
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Trigger:** {skill.Trigger}");
            sb.AppendLine();
        }

        if (skill.Agents.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Agents:** {string.Join(", ", skill.Agents)}");
            sb.AppendLine();
        }

        if (skill.Tools.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Tools:** {string.Join(", ", skill.Tools)}");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(skill.Body))
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Workflow");
            sb.AppendLine();
            sb.Append(skill.Body);
        }

        return sb.ToString();
    }
}

public partial class SkillItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _trigger = string.Empty;

    [ObservableProperty]
    private int _agentCount;

    [ObservableProperty]
    private int _toolCount;

    public IReadOnlyList<string> Agents { get; init; } = [];
    public IReadOnlyList<string> Tools { get; init; } = [];
    public string Body { get; init; } = string.Empty;
    public string Markdown { get; set; } = string.Empty;
}
