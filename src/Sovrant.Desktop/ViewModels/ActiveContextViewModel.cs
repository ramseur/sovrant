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

    private static readonly string UserId =
        Environment.GetEnvironmentVariable("SOVRANT_USER_ID") ?? Environment.UserName;

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
            var personal = Workspaces.FirstOrDefault(w => w.Type.Equals("personal", StringComparison.OrdinalIgnoreCase));
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
        Environment.SetEnvironmentVariable("SOVRANT_WORKSPACE_ID", value.Id);
        Environment.SetEnvironmentVariable("SOVRANT_PROJECT_ID", null);
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
            Environment.SetEnvironmentVariable("SOVRANT_PROJECT_ID", null);
        }
        else
        {
            ActiveProjectId = value.Id;
            ActiveProjectName = value.Name;
            HasActiveProject = true;
            Environment.SetEnvironmentVariable("SOVRANT_PROJECT_ID", value.Id);
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
                // Preserve the current selection by ID across the reload —
                // Clear() would orphan ComboBox.SelectedItem and reset the binding.
                var previouslySelectedId = SelectedWorkspace?.Id ?? ActiveWorkspaceId;

                Workspaces.Clear();
                WorkspaceOption? toReselect = null;
                foreach (var w in workspaces)
                {
                    var option = new WorkspaceOption
                    {
                        Id = w.WorkspaceId,
                        Name = w.Name,
                        Type = w.Type.ToString(),
                    };
                    Workspaces.Add(option);
                    if (option.Id == previouslySelectedId) toReselect = option;
                }

                // If nothing is currently selected (or the prior selection vanished),
                // default to the personal workspace so the user always sees a context.
                if (toReselect is null && string.IsNullOrEmpty(previouslySelectedId))
                {
                    toReselect = Workspaces.FirstOrDefault(
                        w => w.Type.Equals("personal", StringComparison.OrdinalIgnoreCase));
                }

                if (toReselect is not null && !ReferenceEquals(SelectedWorkspace, toReselect))
                {
                    if (string.IsNullOrEmpty(previouslySelectedId))
                    {
                        // No prior context — fire the full change pipeline so projects load.
                        SelectedWorkspace = toReselect;
                    }
                    else
                    {
                        // Restore selection without firing the project-clearing side effects
                        // of OnSelectedWorkspaceChanged — the workspace context is unchanged.
                        SetSelectedWorkspaceSilent(toReselect);
                    }
                }
            });
        }
        catch { /* best effort */ }
    }

    /// <summary>
    /// Update the selected workspace reference without re-running the
    /// "workspace changed" side effects (clearing project, reloading projects, raising
    /// ContextChanged). Used when the underlying collection was just reloaded and the
    /// active workspace itself didn't actually change.
    /// </summary>
    private void SetSelectedWorkspaceSilent(WorkspaceOption option)
    {
        // Set the backing field directly (bypassing the generated setter) so the
        // OnSelectedWorkspaceChanged side effects don't fire, then raise the
        // change notification manually so the ComboBox re-binds.
#pragma warning disable MVVMTK0034
        if (ReferenceEquals(_selectedWorkspace, option)) return;
        _selectedWorkspace = option;
#pragma warning restore MVVMTK0034
        OnPropertyChanged(nameof(SelectedWorkspace));
    }

    private async Task LoadProjectsAsync()
    {
        if (string.IsNullOrEmpty(ActiveWorkspaceId)) return;
        try
        {
            var projects = await _projectService.ListAsync(ActiveWorkspaceId);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Preserve the current project selection by ID across the reload.
                var previouslySelectedId = SelectedProjectChoice?.Id ?? ActiveProjectId;

                Projects.Clear();
                ProjectChoices.Clear();

                var noneOption = new ProjectOption { Id = string.Empty, Name = "(None)", Slug = string.Empty };
                ProjectChoices.Add(noneOption);

                ProjectOption? toReselect = string.IsNullOrEmpty(previouslySelectedId) ? noneOption : null;
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
                    if (opt.Id == previouslySelectedId) toReselect = opt;
                }

                // Default to (None) if the previously-selected project no longer exists.
                toReselect ??= noneOption;

                if (!ReferenceEquals(SelectedProjectChoice, toReselect))
                    SetSelectedProjectSilent(toReselect);
            });
        }
        catch { /* best effort */ }
    }

    private void SetSelectedProjectSilent(ProjectOption option)
    {
#pragma warning disable MVVMTK0034
        if (ReferenceEquals(_selectedProjectChoice, option)) return;
        _selectedProjectChoice = option;
#pragma warning restore MVVMTK0034
        OnPropertyChanged(nameof(SelectedProjectChoice));
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
