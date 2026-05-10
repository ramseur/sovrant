using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Users;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Desktop.ViewModels;

public partial class AdminViewModel : ViewModelBase
{
    private readonly IIdentityService _identity;
    private readonly IUserService _users;
    private readonly IPrincipalAccessor _principal;
    private readonly IWorkspaceService _workspaces;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _error = string.Empty;

    // Users tab
    [ObservableProperty] private string _userFilter = "all";
    public ObservableCollection<User> FilteredUsers { get; } = [];
    private List<User> _allUsers = [];

    // Registration tab
    [ObservableProperty] private bool _registrationOpen;
    [ObservableProperty] private bool _approvalRequired;

    // Password Reset tab
    [ObservableProperty] private User? _selectedResetUser;
    [ObservableProperty] private string _resetToken = string.Empty;
    public ObservableCollection<User> ActiveUsers { get; } = [];

    // Workspaces tab
    [ObservableProperty] private string _newWorkspaceName = string.Empty;
    [ObservableProperty] private Workspace? _selectedWorkspace;
    public ObservableCollection<Workspace> AdminWorkspaces { get; } = [];

    public AdminViewModel(IIdentityService identity, IUserService users, IPrincipalAccessor principal,
        IWorkspaceService workspaces)
    {
        _identity = identity;
        _users = users;
        _principal = principal;
        _workspaces = workspaces;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        Error = string.Empty;
        try
        {
            _allUsers = (await _users.ListAsync().ConfigureAwait(true)).ToList();
            ApplyFilter();
            ActiveUsers.Clear();
            foreach (var u in _allUsers.Where(u => u.Status == "active"))
                ActiveUsers.Add(u);
            RegistrationOpen = await _identity.IsRegistrationOpenAsync().ConfigureAwait(true);
            ApprovalRequired = await _identity.IsApprovalRequiredAsync().ConfigureAwait(true);

            AdminWorkspaces.Clear();
            var allWs = await _workspaces.ListAllAsync().ConfigureAwait(true);
            foreach (var ws in allWs.OrderBy(w => w.Type).ThenBy(w => w.Name))
                AdminWorkspaces.Add(ws);
        }
        catch (Exception ex) { Error = $"Load failed: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    partial void OnUserFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredUsers.Clear();
        var source = UserFilter == "all"
            ? _allUsers
            : _allUsers.Where(u => u.Status == UserFilter);
        foreach (var u in source)
            FilteredUsers.Add(u);
    }

    [RelayCommand]
    private async Task ApproveAsync(User user)
    {
        Status = string.Empty;
        var ok = await _identity.ApproveUserAsync(user.UserId).ConfigureAwait(true);
        Status = ok ? $"{user.Email ?? user.Username} approved." : "Approval failed — user may no longer be pending.";
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DisableAsync(User user)
    {
        if (user.UserId == _principal.UserId) return;
        Status = string.Empty;
        await _users.DeactivateAsync(user.UserId).ConfigureAwait(true);
        Status = $"{user.Email ?? user.Username} disabled.";
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ReactivateAsync(User user)
    {
        Status = string.Empty;
        await _users.ReactivateAsync(user.UserId).ConfigureAwait(true);
        Status = $"{user.Email ?? user.Username} reactivated.";
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleRegistrationAsync()
    {
        await _identity.SetRegistrationOpenAsync(RegistrationOpen).ConfigureAwait(true);
        Status = RegistrationOpen ? "Registration opened." : "Registration closed.";
    }

    [RelayCommand]
    private async Task ToggleApprovalAsync()
    {
        await _identity.SetApprovalRequiredAsync(ApprovalRequired).ConfigureAwait(true);
        Status = ApprovalRequired ? "Approval requirement enabled." : "Approval requirement disabled.";
    }

    [RelayCommand]
    private async Task GenerateResetTokenAsync()
    {
        if (SelectedResetUser is null) return;
        ResetToken = string.Empty;
        Status = string.Empty;
        try
        {
            ResetToken = await _identity.GenerateResetTokenAsync(SelectedResetUser.UserId).ConfigureAwait(true);
        }
        catch (Exception ex) { Error = $"Failed: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task CreateWorkspaceAsync()
    {
        var name = NewWorkspaceName.Trim();
        if (string.IsNullOrEmpty(name)) return;
        Status = string.Empty;
        try
        {
            var slug = name.ToLowerInvariant().Replace(' ', '-');
            await _workspaces.CreateTeamWorkspaceAsync(name, slug, _principal.UserId!).ConfigureAwait(true);
            NewWorkspaceName = string.Empty;
            Status = $"Project '{name}' created.";
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex) { Error = $"Failed: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task DeleteWorkspaceAsync(Workspace ws)
    {
        if (ws.Type == WorkspaceType.Personal) return;
        Status = string.Empty;
        try
        {
            await _workspaces.DeleteAsync(ws.WorkspaceId).ConfigureAwait(true);
            Status = $"'{ws.Name}' deleted.";
            if (SelectedWorkspace?.WorkspaceId == ws.WorkspaceId)
                SelectedWorkspace = null;
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex) { Error = $"Failed: {ex.Message}"; }
    }

}
