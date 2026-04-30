using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
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

    [ObservableProperty]
    private AgentTemplateItemViewModel? _selectedTemplate;

    [ObservableProperty]
    private string _detailMarkdown = string.Empty;

    public ObservableCollection<AgentTemplateItemViewModel> FilteredTemplates { get; } = [];

    public AgentsViewModel(AgentTemplateRegistry registry)
    {
        _registry = registry;
        LoadTemplates();
    }

    [RelayCommand]
    private void Refresh() => LoadTemplates();

    [RelayCommand]
    private void SelectTemplate(AgentTemplateItemViewModel template) => SelectedTemplate = template;

    private void LoadTemplates()
    {
        _allTemplates.Clear();
        foreach (var t in _registry.All.OrderBy(t => t.Name))
        {
            var item = new AgentTemplateItemViewModel
            {
                Name = t.Name,
                Role = t.Role.ToString(),
                RecommendedLevel = t.RecommendedLevel.ToString(),
                ToolCount = t.AllowedTools.Count,
                ToolsSummary = t.AllowedTools.Count > 0
                    ? string.Join(", ", t.AllowedTools.Take(5)) + (t.AllowedTools.Count > 5 ? $" (+{t.AllowedTools.Count - 5} more)" : "")
                    : "All tools",
                AllowedTools = t.AllowedTools,
                SystemPrompt = t.SystemPrompt,
            };
            item.Markdown = BuildAgentMarkdown(item);
            _allTemplates.Add(item);
        }

        TotalCount = _allTemplates.Count;
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedTemplateChanged(AgentTemplateItemViewModel? value)
    {
        DetailMarkdown = value is null ? string.Empty : BuildAgentMarkdown(value);
    }

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

    private static string BuildAgentMarkdown(AgentTemplateItemViewModel agent)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {agent.Name}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Role:** {agent.Role}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Recommended Level:** {agent.RecommendedLevel}");
        sb.AppendLine();

        if (agent.AllowedTools.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Tools ({agent.AllowedTools.Count}):** {string.Join(", ", agent.AllowedTools)}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("**Tools:** All registered tools");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(agent.SystemPrompt))
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## System Prompt");
            sb.AppendLine();
            sb.Append(agent.SystemPrompt);
        }

        return sb.ToString();
    }
}

public partial class AgentTemplateItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _role = string.Empty;
    [ObservableProperty] private string _recommendedLevel = string.Empty;
    [ObservableProperty] private int _toolCount;
    [ObservableProperty] private string _toolsSummary = string.Empty;

    public IReadOnlyList<string> AllowedTools { get; init; } = [];
    public string SystemPrompt { get; init; } = string.Empty;
    public string Markdown { get; set; } = string.Empty;
}
