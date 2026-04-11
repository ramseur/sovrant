using System.Collections.ObjectModel;
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

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];

    public WorkspacesViewModel(IWorkspaceService workspaceService, IUserService userService)
    {
        _workspaceService = workspaceService;
        _userService = userService;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private const string DesktopUserId = "desktop-user";

    private async Task LoadAsync()
    {
        try
        {
            // Ensure the desktop user exists in the users table (FK requirement).
            var user = await _userService.GetAsync(DesktopUserId);
            if (user is null)
            {
                await _userService.CreateAsync("desktop-user", userId: DesktopUserId);
            }

            // Ensure a personal workspace exists for the desktop user.
            var personal = await _workspaceService.GetPersonalAsync(DesktopUserId);
            if (personal is null)
            {
                await _workspaceService.CreatePersonalWorkspaceAsync(DesktopUserId);
            }

            var workspaces = await _workspaceService.ListForUserAsync(DesktopUserId);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Workspaces.Clear();
                foreach (var w in workspaces)
                {
                    Workspaces.Add(new WorkspaceItemViewModel
                    {
                        Id = w.WorkspaceId,
                        Name = w.Name,
                        Slug = w.Slug,
                        IsPersonal = w.Type == WorkspaceType.Personal,
                        CreatedAt = w.CreatedAt,
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

public partial class WorkspaceItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _slug = string.Empty;
    [ObservableProperty] private bool _isPersonal;
    [ObservableProperty] private DateTimeOffset _createdAt;
}
