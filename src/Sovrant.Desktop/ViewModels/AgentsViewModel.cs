using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Agents.Models;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Templates;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Storage;

namespace Sovrant.Desktop.ViewModels;

public partial class AgentsViewModel : ViewModelBase
{
    private readonly AgentTemplateRegistry _registry;
    private readonly AgentDefinitionWriter _writer;
    private readonly AdHocAgentRunner _runner;
    private readonly IAgentRunStore _runStore;
    private readonly IAuditStore _auditStore;
    private readonly ActiveContextViewModel _activeContext;
    private readonly IPrincipalAccessor _principal;
    private readonly Action<string, string?>? _launchChat;
    private readonly List<AgentTemplateItemViewModel> _allTemplates = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private AgentTemplateItemViewModel? _selectedTemplate;
    [ObservableProperty] private string _detailMarkdown = string.Empty;
    [ObservableProperty] private string _runPrompt = string.Empty;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _lastRunStatus = string.Empty;
    [ObservableProperty] private string _lastRunOutput = string.Empty;

    // Editor state
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _editError = string.Empty;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editDescription = string.Empty;
    [ObservableProperty] private string _editRole = "General";
    [ObservableProperty] private string _editLevel = "Standard";
    [ObservableProperty] private string _editTools = string.Empty;
    [ObservableProperty] private string _editPrompt = string.Empty;

    public ObservableCollection<AgentTemplateItemViewModel> FilteredTemplates { get; } = [];
    public ObservableCollection<RecentAgentRunViewModel> RecentRuns { get; } = [];
    public IReadOnlyList<string> RoleNames { get; } = Enum.GetNames<AgentRole>();
    public IReadOnlyList<string> LevelNames { get; } = Enum.GetNames<RecommendedLevel>();

    public AgentsViewModel(
        AgentTemplateRegistry registry,
        AgentDefinitionWriter writer,
        AdHocAgentRunner runner,
        IAgentRunStore runStore,
        IAuditStore auditStore,
        ActiveContextViewModel activeContext,
        IPrincipalAccessor principal,
        Action<string, string?>? launchChat = null)
    {
        _registry = registry;
        _writer = writer;
        _runner = runner;
        _runStore = runStore;
        _auditStore = auditStore;
        _activeContext = activeContext;
        _principal = principal;
        _launchChat = launchChat;
        LoadTemplates();
        _ = LoadRecentRunsAsync();
    }

    /// <summary>
    /// Agents are written to the installation-wide (global) tier, so they are system-wide
    /// knowledge. Per policy, only admins may create, edit, clone, or delete them.
    /// </summary>
    public bool IsAdmin => _principal.IsAdmin;

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    private void Refresh()
    {
        LoadTemplates();
        _ = LoadRecentRunsAsync();
    }

    [RelayCommand]
    private void SelectTemplate(AgentTemplateItemViewModel template)
    {
        SelectedTemplate = template;
        IsEditing = false;
    }

    [RelayCommand(CanExecute = nameof(IsAdmin))]
    private void NewAgent()
    {
        SelectedTemplate = null;
        IsNew = true;
        IsEditing = true;
        EditName = "new-agent";
        EditDescription = string.Empty;
        EditRole = "General";
        EditLevel = "Standard";
        EditTools = string.Empty;
        EditPrompt = "You are a helpful assistant.";
        EditError = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(IsAdmin))]
    private void EditAgent()
    {
        if (SelectedTemplate is null || !_principal.IsAdmin) return;
        IsNew = false;
        IsEditing = true;
        EditName = SelectedTemplate.IsBuiltIn ? $"{SelectedTemplate.Name}-custom" : SelectedTemplate.Name;
        EditDescription = SelectedTemplate.Description;
        EditRole = SelectedTemplate.Role;
        EditLevel = SelectedTemplate.RecommendedLevel;
        EditTools = string.Join(", ", SelectedTemplate.AllowedTools);
        EditPrompt = SelectedTemplate.SystemPrompt;
        EditError = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(IsAdmin))]
    private void CloneAgent()
    {
        if (SelectedTemplate is null || !_principal.IsAdmin) return;
        IsNew = true;
        IsEditing = true;
        EditName = $"{SelectedTemplate.Name}-copy";
        EditDescription = SelectedTemplate.Description;
        EditRole = SelectedTemplate.Role;
        EditLevel = SelectedTemplate.RecommendedLevel;
        EditTools = string.Join(", ", SelectedTemplate.AllowedTools);
        EditPrompt = SelectedTemplate.SystemPrompt;
        EditError = string.Empty;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        IsNew = false;
        EditError = string.Empty;
    }

    [RelayCommand]
    private async Task SaveAgentAsync()
    {
        EditError = string.Empty;
        if (!_principal.IsAdmin) { EditError = "Admin role required to modify agents."; return; }
        var name = AgentDefinitionWriter.ToKebabCase(EditName);
        if (string.IsNullOrWhiteSpace(name)) { EditError = "Name is required."; return; }
        if (string.IsNullOrWhiteSpace(EditPrompt)) { EditError = "System prompt is required."; return; }

        if (!Enum.TryParse<AgentRole>(EditRole, out var role)) role = AgentRole.General;
        if (!Enum.TryParse<RecommendedLevel>(EditLevel, out var level)) level = RecommendedLevel.Standard;

        var tools = string.IsNullOrWhiteSpace(EditTools)
            ? (IReadOnlyList<string>)[]
            : EditTools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var template = new AgentTemplate(
            Name: name,
            Role: role,
            RecommendedLevel: level,
            AllowedTools: tools,
            SystemPrompt: EditPrompt.Trim(),
            IsBuiltIn: false,
            Description: string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim());

        IsSaving = true;
        try
        {
            await _writer.SaveAsync(template).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LoadTemplates();
                SelectedTemplate = FilteredTemplates.FirstOrDefault(t => t.Name == name);
                // Dismiss editor only after list is confirmed updated.
                IsEditing = false;
                IsNew = false;
            });
        }
        catch (Exception ex)
        {
            EditError = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteAgent))]
    private void DeleteAgent()
    {
        if (SelectedTemplate is null || SelectedTemplate.IsBuiltIn || !_principal.IsAdmin) return;
        _writer.Delete(SelectedTemplate.Name);
        SelectedTemplate = null;
        LoadTemplates();
    }

    private bool CanDeleteAgent() => SelectedTemplate is { IsBuiltIn: false } && _principal.IsAdmin;

    [RelayCommand(CanExecute = nameof(CanLaunchChat))]
    private void LaunchChat()
    {
        if (SelectedTemplate is null || _launchChat is null) return;
        _launchChat(SelectedTemplate.Name, SelectedTemplate.SystemPrompt);
    }

    private bool CanLaunchChat() => SelectedTemplate is not null && _launchChat is not null;

    [RelayCommand]
    private async Task RunNowAsync()
    {
        if (SelectedTemplate is null || string.IsNullOrWhiteSpace(RunPrompt) || IsRunning) return;

        IsRunning = true;
        LastRunStatus = "Starting…";
        LastRunOutput = string.Empty;

        try
        {
            var result = await _runner.RunAsync(
                templateName: SelectedTemplate.Name,
                prompt: RunPrompt,
                workspaceId: _activeContext.ActiveWorkspaceId,
                projectId: string.IsNullOrEmpty(_activeContext.ActiveProjectId) ? null : _activeContext.ActiveProjectId,
                userId: App.SovrantUserId).ConfigureAwait(false);

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

    [RelayCommand]
    private async Task TogglePrivacyAsync(RecentAgentRunViewModel? run)
    {
        if (run is null || !run.IsOwn) return;
        var newValue = !run.IsPrivate;
        run.IsPrivate = newValue;
        try
        {
            await _runStore.UpdatePrivacyAsync(run.RunId, App.SovrantUserId, newValue).ConfigureAwait(false);
            await _auditStore.LogPrivacyChangeAsync(App.SovrantUserId, "agent_run", run.RunId, newValue).ConfigureAwait(false);
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() => run.IsPrivate = !newValue);
            throw;
        }
    }

    // ── Data ──────────────────────────────────────────────────────────────

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
                    var title = !string.IsNullOrEmpty(r.Prompt) ? r.Prompt
                        : !string.IsNullOrEmpty(r.MemberId) ? r.MemberId
                        : r.Kind;
                    RecentRuns.Add(new RecentAgentRunViewModel
                    {
                        RunId = r.RunId,
                        Title = title,
                        AgentName = r.MemberId ?? string.Empty,
                        Status = r.Status,
                        WhenLabel = FormatRelative(r.EndedAt ?? r.StartedAt),
                        IsPrivate = r.IsPrivate,
                        IsOwn = string.Equals(r.UserId, App.SovrantUserId, StringComparison.Ordinal),
                    });
                }
            });
        }
        catch { }
    }

    private void LoadTemplates()
    {
        _allTemplates.Clear();
        foreach (var t in _registry.All
            .OrderBy(t => t.IsBuiltIn ? 1 : 0)
            .ThenBy(t => t.Name))
        {
            var item = new AgentTemplateItemViewModel
            {
                Name = t.Name,
                Description = t.Description ?? string.Empty,
                Role = t.Role.ToString(),
                RecommendedLevel = t.RecommendedLevel.ToString(),
                ToolCount = t.AllowedTools.Count,
                ToolsSummary = t.AllowedTools.Count > 0
                    ? string.Join(", ", t.AllowedTools.Take(5)) + (t.AllowedTools.Count > 5 ? $" (+{t.AllowedTools.Count - 5} more)" : "")
                    : "All tools",
                AllowedTools = t.AllowedTools,
                SystemPrompt = t.SystemPrompt,
                IsBuiltIn = t.IsBuiltIn,
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
        IsEditing = false;
        DeleteAgentCommand.NotifyCanExecuteChanged();
        LaunchChatCommand.NotifyCanExecuteChanged();
    }

    private void ApplyFilter()
    {
        FilteredTemplates.Clear();
        var query = SearchText.Trim();
        foreach (var t in _allTemplates)
        {
            if (query.Length > 0
                && !t.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !t.Role.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !t.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;
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
        if (!string.IsNullOrWhiteSpace(agent.Description))
        {
            sb.AppendLine();
            sb.AppendLine(agent.Description);
        }
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Role:** {agent.Role}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Level:** {agent.RecommendedLevel}");
        sb.AppendLine();
        sb.AppendLine(agent.AllowedTools.Count > 0
            ? string.Create(CultureInfo.InvariantCulture,
                $"**Tools ({agent.AllowedTools.Count}):** {string.Join(", ", agent.AllowedTools)}")
            : "**Tools:** All registered tools");
        sb.AppendLine();
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
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _role = string.Empty;
    [ObservableProperty] private string _recommendedLevel = string.Empty;
    [ObservableProperty] private int _toolCount;
    [ObservableProperty] private string _toolsSummary = string.Empty;
    [ObservableProperty] private bool _isBuiltIn;

    public IReadOnlyList<string> AllowedTools { get; init; } = [];
    public string SystemPrompt { get; init; } = string.Empty;
    public string Markdown { get; set; } = string.Empty;
}

public partial class RecentAgentRunViewModel : ViewModelBase
{
    [ObservableProperty] private string _runId = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _agentName = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _whenLabel = string.Empty;
    [ObservableProperty] private bool _isPrivate;
    [ObservableProperty] private bool _isOwn;
}
