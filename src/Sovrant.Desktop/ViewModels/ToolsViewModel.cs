using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Tools;

namespace Sovrant.Desktop.ViewModels;

public partial class ToolsViewModel : ViewModelBase
{
    private readonly IToolRegistry _registry;
    private readonly List<ToolItemViewModel> _allTools = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    public ObservableCollection<ToolItemViewModel> FilteredTools { get; } = [];

    public ToolsViewModel(IToolRegistry registry)
    {
        _registry = registry;
        LoadTools();
    }

    [RelayCommand]
    private void Refresh() => LoadTools();

    private void LoadTools()
    {
        _allTools.Clear();
        foreach (var def in _registry.GetDefinitions().OrderBy(d => d.Name))
        {
            _allTools.Add(new ToolItemViewModel
            {
                Name = def.Name,
                Description = def.Description ?? "(no description)",
            });
        }

        TotalCount = _allTools.Count;
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredTools.Clear();
        var query = SearchText.Trim();

        foreach (var tool in _allTools)
        {
            if (query.Length > 0
                && !tool.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !tool.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FilteredTools.Add(tool);
        }
    }
}

public partial class ToolItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;
}
