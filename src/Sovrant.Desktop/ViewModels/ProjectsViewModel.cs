using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Projects;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Desktop.ViewModels;

public partial class ProjectsViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly List<ProjectItemViewModel> _allProjects = [];

    private static readonly string PersonalWorkspaceId = WorkspaceIdentity.DefaultPersonal();

    [ObservableProperty]
    private string _newProjectName = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private ProjectItemViewModel? _selectedProject;

    [ObservableProperty]
    private string _detailMarkdown = string.Empty;

    public ObservableCollection<ProjectItemViewModel> FilteredProjects { get; } = [];

    public ProjectsViewModel(IProjectService projectService)
    {
        _projectService = projectService;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void SelectProject(ProjectItemViewModel project) => SelectedProject = project;

    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        var name = NewProjectName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        try
        {
#pragma warning disable CA1308 // Slugs must be lowercase
            var slug = name.ToLowerInvariant().Replace(' ', '-');
#pragma warning restore CA1308
            await _projectService.CreateAsync(PersonalWorkspaceId, name, slug);
            NewProjectName = string.Empty;
            StatusMessage = $"Created project '{name}'.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteProjectAsync(ProjectItemViewModel project)
    {
        await _projectService.DeleteAsync(project.Id);
        if (SelectedProject == project) SelectedProject = null;
        StatusMessage = $"Deleted project '{project.Name}'.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ArchiveProjectAsync(ProjectItemViewModel project)
    {
        if (project.IsArchived)
        {
            await _projectService.UnarchiveAsync(project.Id);
            StatusMessage = $"Unarchived '{project.Name}'.";
        }
        else
        {
            await _projectService.ArchiveAsync(project.Id);
            StatusMessage = $"Archived '{project.Name}'.";
        }
        await LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedProjectChanged(ProjectItemViewModel? value)
    {
        DetailMarkdown = value is null ? string.Empty : BuildProjectMarkdown(value);
    }

    private async Task LoadAsync()
    {
        try
        {
            var projects = await _projectService.ListAsync(PersonalWorkspaceId, includeArchived: true);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _allProjects.Clear();
                foreach (var p in projects)
                {
                    var item = new ProjectItemViewModel
                    {
                        Id = p.ProjectId,
                        Name = p.Name,
                        Slug = p.Slug,
                        Description = p.Description ?? string.Empty,
                        CreatedAt = p.CreatedAt,
                        IsArchived = p.ArchivedAt.HasValue,
                    };
                    item.Markdown = BuildProjectMarkdown(item);
                    _allProjects.Add(item);
                }
                TotalCount = _allProjects.Count;
                ApplyFilter();
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load error: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        FilteredProjects.Clear();
        var query = SearchText.Trim();

        foreach (var p in _allProjects)
        {
            if (query.Length > 0
                && !p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !p.Slug.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FilteredProjects.Add(p);
        }
    }

    private static string BuildProjectMarkdown(ProjectItemViewModel project)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {project.Name}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Slug:** {project.Slug}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**ID:** {project.Id}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Created:** {project.CreatedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Status:** {(project.IsArchived ? "Archived" : "Active")}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(project.Description))
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine(project.Description);
        }

        return sb.ToString();
    }
}

public partial class ProjectItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _slug = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private DateTimeOffset _createdAt;
    [ObservableProperty] private bool _isArchived;
    public string Markdown { get; set; } = string.Empty;
}
