using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Users;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Desktop.ViewModels;

public partial class WorkspacesViewModel : ViewModelBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IUserService _userService;
    private readonly ActiveContextViewModel _activeContext;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _newWorkspaceName = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private WorkspaceItemViewModel? _selectedWorkspace;

    [ObservableProperty]
    private string _detailMarkdown = string.Empty;

    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];

    private static string DesktopUserId => App.SovrantUserId;

    public WorkspacesViewModel(IWorkspaceService workspaceService, IUserService userService, ActiveContextViewModel activeContext)
    {
        _workspaceService = workspaceService;
        _userService = userService;
        _activeContext = activeContext;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void SelectWorkspace(WorkspaceItemViewModel workspace) => SelectedWorkspace = workspace;

    [RelayCommand]
    private async Task CreateWorkspaceAsync()
    {
        var name = NewWorkspaceName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        try
        {
#pragma warning disable CA1308 // Slugs must be lowercase
            var slug = name.ToLowerInvariant().Replace(' ', '-');
#pragma warning restore CA1308
            await _workspaceService.CreateTeamWorkspaceAsync(name, slug, DesktopUserId);
            NewWorkspaceName = string.Empty;
            StatusMessage = $"Created workspace '{name}'.";
            await LoadAsync();
            await _activeContext.RefreshCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteWorkspaceAsync(WorkspaceItemViewModel workspace)
    {
        if (workspace.IsPersonal)
        {
            StatusMessage = "Cannot delete personal workspace.";
            return;
        }

        var success = await _workspaceService.DeleteAsync(workspace.Id);
        if (success)
        {
            if (SelectedWorkspace == workspace) SelectedWorkspace = null;
            StatusMessage = $"Deleted workspace '{workspace.Name}'.";
            await LoadAsync();
            await _activeContext.RefreshCommand.ExecuteAsync(null);
        }
        else
        {
            StatusMessage = "Failed to delete workspace.";
        }
    }

    partial void OnSelectedWorkspaceChanged(WorkspaceItemViewModel? value)
    {
        DetailMarkdown = value is null ? string.Empty : BuildWorkspaceMarkdown(value);
    }

    private async Task LoadAsync()
    {
        try
        {
            var user = await _userService.GetAsync(DesktopUserId);
            if (user is null)
                await _userService.CreateAsync(DesktopUserId, userId: DesktopUserId);

            var personal = await _workspaceService.GetPersonalAsync(DesktopUserId);
            if (personal is null)
                await _workspaceService.CreatePersonalWorkspaceAsync(DesktopUserId);

            var workspaces = await _workspaceService.ListForUserAsync(DesktopUserId);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Workspaces.Clear();
                foreach (var w in workspaces)
                {
                    var item = new WorkspaceItemViewModel
                    {
                        Id = w.WorkspaceId,
                        Name = w.Name,
                        Slug = w.Slug,
                        IsPersonal = w.Type == WorkspaceType.Personal,
                        CreatedAt = w.CreatedAt,
                    };
                    item.Markdown = BuildWorkspaceMarkdown(item);
                    Workspaces.Add(item);
                }
                TotalCount = Workspaces.Count;
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load error: {ex.Message}";
        }
    }

    private static string BuildWorkspaceMarkdown(WorkspaceItemViewModel workspace)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {workspace.Name}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Slug:** {workspace.Slug}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**ID:** {workspace.Id}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Type:** {(workspace.IsPersonal ? "Personal" : "Team")}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Created:** {workspace.CreatedAt:yyyy-MM-dd HH:mm}");

        return sb.ToString();
    }
}

public partial class WorkspaceItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _slug = string.Empty;
    [ObservableProperty] private bool _isPersonal;
    [ObservableProperty] private DateTimeOffset _createdAt;
    public string Markdown { get; set; } = string.Empty;
}
