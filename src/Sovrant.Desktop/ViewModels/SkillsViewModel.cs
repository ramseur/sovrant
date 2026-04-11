using System.Collections.ObjectModel;
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

    public ObservableCollection<SkillItemViewModel> FilteredSkills { get; } = [];

    public SkillsViewModel(SkillRegistry registry)
    {
        _registry = registry;
        LoadSkills();
    }

    [RelayCommand]
    private void Refresh() => LoadSkills();

    private void LoadSkills()
    {
        _allSkills.Clear();
        foreach (var s in _registry.All.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            _allSkills.Add(new SkillItemViewModel
            {
                Name = s.Name,
                Description = s.Description,
                Trigger = string.IsNullOrEmpty(s.Trigger) ? "(none)" : s.Trigger,
                AgentCount = s.Agents.Count,
                ToolCount = s.Tools.Count,
            });
        }

        TotalCount = _allSkills.Count;
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

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
}
