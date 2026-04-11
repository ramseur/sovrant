using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Projects;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Tracks the active workspace and project context. Shared as a singleton
/// across sidebar, chat, and other views so the user always knows what
/// context they're operating in.
/// </summary>
public partial class ActiveContextViewModel : ViewModelBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IProjectService _projectService;

    private const string UserId = "desktop-user";

    [ObservableProperty]
    private string _activeWorkspaceId = string.Empty;

    [ObservableProperty]
    private string _activeWorkspaceName = "Personal";

    [ObservableProperty]
    private string _activeProjectId = string.Empty;

    [ObservableProperty]
    private string _activeProjectName = "No Project";

    [ObservableProperty]
    private bool _hasActiveProject;

    [ObservableProperty]
    private WorkspaceOption? _selectedWorkspace;

    [ObservableProperty]
    private ProjectOption? _selectedProjectChoice;

    public ObservableCollection<WorkspaceOption> Workspaces { get; } = [];
    public ObservableCollection<ProjectOption> Projects { get; } = [];
    public ObservableCollection<ProjectOption> ProjectChoices { get; } = [];

    /// <summary>Raised when the active context changes (workspace or project).</summary>
    public event Action? ContextChanged;

    public string ContextDisplay => HasActiveProject
        ? $"{ActiveWorkspaceName} > {ActiveProjectName}"
        : ActiveWorkspaceName;

    public ActiveContextViewModel(IWorkspaceService workspaceService, IProjectService projectService)
    {
        _workspaceService = workspaceService;
        _projectService = projectService;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Wait for runtime (DB migrations) before querying workspaces.
            await App.RuntimeReady.Task.ConfigureAwait(false);
            await LoadWorkspacesAsync();

            // Default to personal workspace.
            var personal = Workspaces.FirstOrDefault(w => w.Type == "personal");
            if (personal is not null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => SelectedWorkspace = personal);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ActiveContext init failed: {ex.Message}");
        }
    }

    partial void OnSelectedWorkspaceChanged(WorkspaceOption? value)
    {
        if (value is null) return;
        ActiveWorkspaceId = value.Id;
        ActiveWorkspaceName = value.Name;
        ActiveProjectId = string.Empty;
        ActiveProjectName = "No Project";
        HasActiveProject = false;
        OnPropertyChanged(nameof(ContextDisplay));
        _ = LoadProjectsAsync();
        ContextChanged?.Invoke();
    }

    partial void OnSelectedProjectChoiceChanged(ProjectOption? value)
    {
        if (value is null) return;
        if (value.Id == string.Empty)
        {
            // "None" option selected
            ActiveProjectId = string.Empty;
            ActiveProjectName = "No Project";
            HasActiveProject = false;
        }
        else
        {
            ActiveProjectId = value.Id;
            ActiveProjectName = value.Name;
            HasActiveProject = true;
        }
        OnPropertyChanged(nameof(ContextDisplay));
        ContextChanged?.Invoke();
    }

    [RelayCommand]
    private void SelectWorkspace(WorkspaceOption workspace)
    {
        SelectedWorkspace = workspace;
    }

    [RelayCommand]
    private void SelectProject(ProjectOption project)
    {
        SelectedProjectChoice = project;
    }

    [RelayCommand]
    private void ClearProject()
    {
        var none = ProjectChoices.FirstOrDefault(p => string.IsNullOrEmpty(p.Id));
        if (none is not null)
            SelectedProjectChoice = none;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadWorkspacesAsync();
        await LoadProjectsAsync();
    }

    private async Task LoadWorkspacesAsync()
    {
        try
        {
            var workspaces = await _workspaceService.ListForUserAsync(UserId);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Workspaces.Clear();
                foreach (var w in workspaces)
                {
                    Workspaces.Add(new WorkspaceOption
                    {
                        Id = w.WorkspaceId,
                        Name = w.Name,
                        Type = w.Type.ToString(),
                    });
                }
            });
        }
        catch { /* best effort */ }
    }

    private async Task LoadProjectsAsync()
    {
        if (string.IsNullOrEmpty(ActiveWorkspaceId)) return;
        try
        {
            var projects = await _projectService.ListAsync(ActiveWorkspaceId);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Projects.Clear();
                ProjectChoices.Clear();

                var noneOption = new ProjectOption { Id = string.Empty, Name = "(None)", Slug = string.Empty };
                ProjectChoices.Add(noneOption);

                foreach (var p in projects)
                {
                    var opt = new ProjectOption
                    {
                        Id = p.ProjectId,
                        Name = p.Name,
                        Slug = p.Slug,
                    };
                    Projects.Add(opt);
                    ProjectChoices.Add(opt);
                }

                SelectedProjectChoice = noneOption;
            });
        }
        catch { /* best effort */ }
    }
}

public sealed class WorkspaceOption
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
}

public sealed class ProjectOption
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
}
