using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Users;

namespace Sovrant.Desktop.ViewModels;

public partial class AdminViewModel : ViewModelBase
{
    private readonly IIdentityService _identity;
    private readonly IUserService _users;
    private readonly IPrincipalAccessor _principal;

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

    public AdminViewModel(IIdentityService identity, IUserService users, IPrincipalAccessor principal)
    {
        _identity = identity;
        _users = users;
        _principal = principal;
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

}
