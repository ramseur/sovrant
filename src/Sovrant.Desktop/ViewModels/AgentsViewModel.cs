using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Agents.Templates;

namespace Sovrant.Desktop.ViewModels;

public partial class AgentsViewModel : ViewModelBase
{
    private readonly AgentTemplateRegistry _registry;
    private readonly List<AgentTemplateItemViewModel> _allTemplates = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    public ObservableCollection<AgentTemplateItemViewModel> FilteredTemplates { get; } = [];

    public AgentsViewModel(AgentTemplateRegistry registry)
    {
        _registry = registry;
        LoadTemplates();
    }

    [RelayCommand]
    private void Refresh() => LoadTemplates();

    private void LoadTemplates()
    {
        _allTemplates.Clear();
        foreach (var t in _registry.All.OrderBy(t => t.Name))
        {
            _allTemplates.Add(new AgentTemplateItemViewModel
            {
                Name = t.Name,
                Role = t.Role.ToString(),
                RecommendedLevel = t.RecommendedLevel.ToString(),
                ToolCount = t.AllowedTools.Count,
                ToolsSummary = t.AllowedTools.Count > 0
                    ? string.Join(", ", t.AllowedTools.Take(5)) + (t.AllowedTools.Count > 5 ? $" (+{t.AllowedTools.Count - 5} more)" : "")
                    : "All tools",
            });
        }

        TotalCount = _allTemplates.Count;
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredTemplates.Clear();
        var query = SearchText.Trim();

        foreach (var t in _allTemplates)
        {
            if (query.Length > 0
                && !t.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !t.Role.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FilteredTemplates.Add(t);
        }
    }
}

public partial class AgentTemplateItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _role = string.Empty;
    [ObservableProperty] private string _recommendedLevel = string.Empty;
    [ObservableProperty] private int _toolCount;
    [ObservableProperty] private string _toolsSummary = string.Empty;
}
