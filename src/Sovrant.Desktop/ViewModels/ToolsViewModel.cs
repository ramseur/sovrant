using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
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

    [ObservableProperty]
    private ToolItemViewModel? _selectedTool;

    [ObservableProperty]
    private string _detailMarkdown = string.Empty;

    public ObservableCollection<ToolItemViewModel> FilteredTools { get; } = [];

    public ToolsViewModel(IToolRegistry registry)
    {
        _registry = registry;
        LoadTools();
    }

    [RelayCommand]
    private void Refresh() => LoadTools();

    [RelayCommand]
    private void SelectTool(ToolItemViewModel tool) => SelectedTool = tool;

    private void LoadTools()
    {
        _allTools.Clear();
        foreach (var def in _registry.GetDefinitions().OrderBy(d => d.Name))
        {
            var item = new ToolItemViewModel
            {
                Name = def.Name,
                Description = def.Description ?? "(no description)",
                InputSchemaJson = def.InputSchema,
            };
            item.Markdown = BuildToolMarkdown(item);
            _allTools.Add(item);
        }

        TotalCount = _allTools.Count;
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedToolChanged(ToolItemViewModel? value)
    {
        DetailMarkdown = value is null ? string.Empty : BuildToolMarkdown(value);
    }

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

    private static string BuildToolMarkdown(ToolItemViewModel tool)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {tool.Name}");
        sb.AppendLine();
        sb.AppendLine(tool.Description);
        sb.AppendLine();

        // Parse input schema for parameters
        if (tool.InputSchemaJson.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine("## Parameters");
            sb.AppendLine();

            if (tool.InputSchemaJson.TryGetProperty("properties", out var props)
                && props.ValueKind == JsonValueKind.Object)
            {
                // Get required list
                var required = new HashSet<string>(StringComparer.Ordinal);
                if (tool.InputSchemaJson.TryGetProperty("required", out var reqArr)
                    && reqArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in reqArr.EnumerateArray())
                        if (r.GetString() is { } name) required.Add(name);
                }

                sb.AppendLine("| Parameter | Type | Required | Description |");
                sb.AppendLine("|-----------|------|----------|-------------|");

                foreach (var prop in props.EnumerateObject())
                {
                    var paramName = prop.Name;
                    var paramType = prop.Value.TryGetProperty("type", out var t) ? t.GetString() ?? "any" : "any";
                    var paramDesc = prop.Value.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    var isRequired = required.Contains(paramName) ? "Yes" : "No";

                    // Escape pipes in description
                    paramDesc = paramDesc.Replace("|", "\\|", StringComparison.Ordinal);

                    sb.AppendLine(CultureInfo.InvariantCulture, $"| {paramName} | {paramType} | {isRequired} | {paramDesc} |");
                }
            }
            else
            {
                sb.AppendLine("No parameters");
            }
        }

        return sb.ToString();
    }
}

public partial class ToolItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    public JsonElement InputSchemaJson { get; init; }

    public string Markdown { get; set; } = string.Empty;
}
