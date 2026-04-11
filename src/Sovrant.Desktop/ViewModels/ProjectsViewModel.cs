using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Projects;

namespace Sovrant.Desktop.ViewModels;

public partial class ProjectsViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;

    [ObservableProperty]
    private string _newProjectName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = [];

    public ProjectsViewModel(IProjectService projectService)
    {
        _projectService = projectService;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

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
            await _projectService.CreateAsync("personal", name, slug);
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
    private async Task DeleteProjectAsync(string projectId)
    {
        await _projectService.DeleteAsync(projectId);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var projects = await _projectService.ListAsync("personal");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Projects.Clear();
                foreach (var p in projects)
                {
                    Projects.Add(new ProjectItemViewModel
                    {
                        Id = p.ProjectId,
                        Name = p.Name,
                        Slug = p.Slug,
                        Description = p.Description ?? string.Empty,
                        CreatedAt = p.CreatedAt,
                        IsArchived = p.ArchivedAt.HasValue,
                    });
                }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load error: {ex.Message}";
        }
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
}
