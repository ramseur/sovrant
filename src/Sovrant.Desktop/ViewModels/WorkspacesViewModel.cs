using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Desktop.ViewModels;

public partial class WorkspacesViewModel : ViewModelBase
{
    private readonly IWorkspaceService _workspaceService;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];

    public WorkspacesViewModel(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            // Desktop uses a default user context.
            var workspaces = await _workspaceService.ListForUserAsync("desktop-user");
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
