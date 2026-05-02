using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Templates;
using Sovrant.Runtime.Storage;

namespace Sovrant.Desktop.ViewModels;

public partial class AgentsViewModel : ViewModelBase
{
    private readonly AgentTemplateRegistry _registry;
    private readonly AdHocAgentRunner _runner;
    private readonly IAgentRunStore _runStore;
    private readonly ActiveContextViewModel _activeContext;
    private readonly List<AgentTemplateItemViewModel> _allTemplates = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private AgentTemplateItemViewModel? _selectedTemplate;

    [ObservableProperty]
    private string _detailMarkdown = string.Empty;

    [ObservableProperty]
    private string _runPrompt = string.Empty;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _lastRunStatus = string.Empty;

    [ObservableProperty]
    private string _lastRunOutput = string.Empty;

    public ObservableCollection<AgentTemplateItemViewModel> FilteredTemplates { get; } = [];
    public ObservableCollection<RecentAgentRunViewModel> RecentRuns { get; } = [];

    public AgentsViewModel(
        AgentTemplateRegistry registry,
        AdHocAgentRunner runner,
        IAgentRunStore runStore,
        ActiveContextViewModel activeContext)
    {
        _registry = registry;
        _runner = runner;
        _runStore = runStore;
        _activeContext = activeContext;
        LoadTemplates();
        _ = LoadRecentRunsAsync();
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadTemplates();
        _ = LoadRecentRunsAsync();
    }

    [RelayCommand]
    private void SelectTemplate(AgentTemplateItemViewModel template) => SelectedTemplate = template;

    [RelayCommand]
    private async Task RunNowAsync()
    {
        if (SelectedTemplate is null || string.IsNullOrWhiteSpace(RunPrompt) || IsRunning) return;

        IsRunning = true;
        LastRunStatus = "Starting…";
        LastRunOutput = string.Empty;

        try
        {
            var workspaceId = _activeContext.ActiveWorkspaceId;
            var projectId = string.IsNullOrEmpty(_activeContext.ActiveProjectId) ? null : _activeContext.ActiveProjectId;
            var userId = App.SovrantUserId;

            var result = await _runner.RunAsync(
                templateName: SelectedTemplate.Name,
                prompt: RunPrompt,
                workspaceId: workspaceId,
                projectId: projectId,
                userId: userId).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LastRunStatus = result.Success ? "Completed" : "Failed";
                LastRunOutput = result.Success ? result.Output : (result.Error ?? "(no error message)");
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LastRunStatus = "Failed";
                LastRunOutput = ex.Message;
            });
        }
        finally
        {
            IsRunning = false;
            await LoadRecentRunsAsync().ConfigureAwait(false);
        }
    }

    private async Task LoadRecentRunsAsync()
    {
        try
        {
            var workspaceId = _activeContext.ActiveWorkspaceId;
            var filter = string.IsNullOrEmpty(workspaceId)
                ? null
                : new AgentRunFilter(WorkspaceId: workspaceId);
            var runs = await _runStore.ListAsync(filter, limit: 10).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RecentRuns.Clear();
                foreach (var r in runs)
                {
                    RecentRuns.Add(new RecentAgentRunViewModel
                    {
                        RunId = r.RunId,
                        Title = string.IsNullOrEmpty(r.MemberId) ? r.Kind : r.MemberId,
                        Status = r.Status,
                        WhenLabel = FormatRelative(r.EndedAt ?? r.StartedAt),
                    });
                }
            });
        }
        catch
        {
            // Best-effort load; silently keep stale list on transient errors.
        }
    }

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
        RunPrompt = string.Empty;
        LastRunStatus = string.Empty;
        LastRunOutput = string.Empty;
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

    private static string FormatRelative(DateTimeOffset when)
    {
        var delta = DateTimeOffset.UtcNow - when;
        if (delta.TotalSeconds < 5) return "just now";
        if (delta.TotalSeconds < 60) return $"{(int)delta.TotalSeconds}s ago";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
        return when.LocalDateTime.ToString("MMM d, HH:mm", CultureInfo.InvariantCulture);
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

public partial class RecentAgentRunViewModel : ViewModelBase
{
    [ObservableProperty] private string _runId = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _whenLabel = string.Empty;
}
