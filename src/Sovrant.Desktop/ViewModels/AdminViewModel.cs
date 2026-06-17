using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Users;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Desktop.ViewModels;

public partial class AdminViewModel : ViewModelBase
{
    private readonly IIdentityService _identity;
    private readonly IUserService _users;
    private readonly IPrincipalAccessor _principal;
    private readonly IWorkspaceService _workspaces;
    private readonly ActiveContextViewModel _activeContext;
    private readonly IWorkspaceSettingsStore _wsSettings;
    private readonly IProviderProfileStore _profileStore;
    private readonly IMcpServerStore _mcpServerStore;

    /// <summary>Set by the View to show a simple confirmation before disabling a user.</summary>
    public Func<string, string, Task<bool>>? ConfirmDisableAsync { get; set; }

    /// <summary>Set by the View to show a type-name confirmation before permanently deleting a user.</summary>
    public Func<string, string, Task<bool>>? ConfirmDeleteAsync { get; set; }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _error = string.Empty;

    // Top-level section ("users" = Users/Registration/PW-Reset, "workspaces" = Workspaces).
    [ObservableProperty] private string _section = "users";
    public bool IsUsersSection => Section == "users";
    public bool IsWorkspacesSection => Section == "workspaces";

    partial void OnSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsUsersSection));
        OnPropertyChanged(nameof(IsWorkspacesSection));
    }

    // Sub-tab state within the Users section (0 = Users, 1 = Registration, 2 = Password Reset).
    [ObservableProperty] private int _selectedTab;
    public bool IsUsersTab => SelectedTab == 0;
    public bool IsRegistrationTab => SelectedTab == 1;
    public bool IsPasswordResetTab => SelectedTab == 2;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsUsersTab));
        OnPropertyChanged(nameof(IsRegistrationTab));
        OnPropertyChanged(nameof(IsPasswordResetTab));
    }

    [RelayCommand]
    private void SelectTab(string tab) => SelectedTab = int.Parse(tab, System.Globalization.CultureInfo.InvariantCulture);

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

    // ── Per-workspace configuration panel ────────────────────────────────────

    // Which workspace is currently being configured (null = panel hidden)
    [ObservableProperty] private Workspace? _configWorkspace;

    // General settings for the config workspace
    [ObservableProperty] private int _wsMaxTokens = 32000;
    [ObservableProperty] private bool _wsStreaming = true;
    [ObservableProperty] private bool _wsIntentRouting;
    [ObservableProperty] private string _wsWebSearchBackend = "Auto";

    // Member management
    [ObservableProperty] private string _newMemberEmail = string.Empty;
    [ObservableProperty] private string _newMemberRole = "Member"; // "Admin" or "Member"

    public ObservableCollection<ConfigWorkspaceMemberRow> ConfigWorkspaceMembers { get; } = [];
    public ObservableCollection<WorkspaceProviderToggleViewModel> ProviderToggles { get; } = [];
    public ObservableCollection<WorkspaceMcpToggleViewModel> McpToggles { get; } = [];

    public bool IsConfigPanelVisible => ConfigWorkspace is not null;

    partial void OnConfigWorkspaceChanged(Workspace? value) => OnPropertyChanged(nameof(IsConfigPanelVisible));

    public IReadOnlyList<string> WebSearchBackends { get; } = ["Auto", "Brave", "Firecrawl", "Native", "Off"];
    public IReadOnlyList<string> MemberRoles { get; } = ["Member", "Admin"];

    public AdminViewModel(IIdentityService identity, IUserService users, IPrincipalAccessor principal,
        IWorkspaceService workspaces, ActiveContextViewModel activeContext,
        IWorkspaceSettingsStore wsSettings, IProviderProfileStore profileStore,
        IMcpServerStore mcpServerStore)
    {
        _identity = identity;
        _users = users;
        _principal = principal;
        _workspaces = workspaces;
        _activeContext = activeContext;
        _wsSettings = wsSettings;
        _profileStore = profileStore;
        _mcpServerStore = mcpServerStore;
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
        if (ConfirmDisableAsync is not null &&
            !await ConfirmDisableAsync("User", user.Email ?? user.Username)) return;
        Status = string.Empty;
        await _users.DeactivateAsync(user.UserId).ConfigureAwait(true);
        Status = $"{user.Email ?? user.Username} disabled.";
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task HardDeleteAsync(User user)
    {
        if (user.UserId == _principal.UserId) return;
        if (ConfirmDeleteAsync is not null &&
            !await ConfirmDeleteAsync("User", user.Email ?? user.Username)) return;
        Status = string.Empty;
        await _users.HardDeleteAsync(user.UserId).ConfigureAwait(true);
        Status = $"{user.Email ?? user.Username} permanently deleted.";
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
#pragma warning disable CA1308
            var slug = name.ToLowerInvariant().Replace(' ', '-');
#pragma warning restore CA1308
            await _workspaces.CreateTeamWorkspaceAsync(name, slug, _principal.UserId!).ConfigureAwait(true);
            NewWorkspaceName = string.Empty;
            Status = $"Workspace '{name}' created.";
            await LoadAsync().ConfigureAwait(true);
            await _activeContext.RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
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
            await _activeContext.RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
        catch (Exception ex) { Error = $"Failed: {ex.Message}"; }
    }

    // ── Per-workspace configuration commands ──────────────────────────────────

    [RelayCommand]
    private async Task ConfigureWorkspaceAsync(Workspace ws)
    {
        if (ConfigWorkspace?.WorkspaceId == ws.WorkspaceId)
        {
            // Toggle off — hide panel
            ConfigWorkspace = null;
            return;
        }
        ConfigWorkspace = ws;
        Status = string.Empty;

        // Load general settings
        var maxRaw = await _wsSettings.GetAsync(ws.WorkspaceId, WorkspaceSettingsKeys.GeneralMaxTokens);
        WsMaxTokens = int.TryParse(maxRaw, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 32000;
        var streamRaw = await _wsSettings.GetAsync(ws.WorkspaceId, WorkspaceSettingsKeys.GeneralStreaming);
        WsStreaming = !string.Equals(streamRaw, "false", StringComparison.OrdinalIgnoreCase);
        var intentRaw = await _wsSettings.GetAsync(ws.WorkspaceId, WorkspaceSettingsKeys.GeneralIntentRouting);
        WsIntentRouting = string.Equals(intentRaw, "true", StringComparison.OrdinalIgnoreCase);
        var wsRaw = await _wsSettings.GetAsync(ws.WorkspaceId, WorkspaceSettingsKeys.GeneralWebSearchBackend);
        WsWebSearchBackend = string.IsNullOrEmpty(wsRaw) ? "Auto" : wsRaw;

        // Load members
        await LoadConfigMembersAsync(ws.WorkspaceId);

        // Load provider toggles
        await LoadProviderTogglesAsync(ws.WorkspaceId);

        // Load MCP toggles
        await LoadMcpTogglesAsync(ws.WorkspaceId);
    }

    [RelayCommand]
    private async Task SaveWorkspaceGeneralAsync()
    {
        if (ConfigWorkspace is null) return;
        var wsId = ConfigWorkspace.WorkspaceId;
        await _wsSettings.SetAsync(wsId, WorkspaceSettingsKeys.GeneralMaxTokens,
            WsMaxTokens.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await _wsSettings.SetAsync(wsId, WorkspaceSettingsKeys.GeneralStreaming,
            WsStreaming ? "true" : "false");
        await _wsSettings.SetAsync(wsId, WorkspaceSettingsKeys.GeneralIntentRouting,
            WsIntentRouting ? "true" : "false");
        await _wsSettings.SetAsync(wsId, WorkspaceSettingsKeys.GeneralWebSearchBackend,
            WsWebSearchBackend);
        Status = "General settings saved.";
    }

    [RelayCommand]
    private async Task AddWorkspaceMemberAsync()
    {
        if (ConfigWorkspace is null || string.IsNullOrWhiteSpace(NewMemberEmail)) return;
        var allUsers = await _users.ListAsync();
        var user = allUsers.FirstOrDefault(u =>
            string.Equals(u.Email, NewMemberEmail.Trim(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(u.UserId, NewMemberEmail.Trim(), StringComparison.OrdinalIgnoreCase));
        if (user is null) { Status = $"User '{NewMemberEmail}' not found."; return; }
        var role = string.Equals(NewMemberRole, "Admin", StringComparison.OrdinalIgnoreCase)
            ? WorkspaceRole.Admin : WorkspaceRole.Member;
        await _workspaces.AddMemberAsync(ConfigWorkspace.WorkspaceId, user.UserId, role);
        NewMemberEmail = string.Empty;
        await LoadConfigMembersAsync(ConfigWorkspace.WorkspaceId);
        Status = $"{user.Email ?? user.UserId} added as {role}.";
    }

    [RelayCommand]
    private async Task RemoveWorkspaceMemberAsync(ConfigWorkspaceMemberRow member)
    {
        if (ConfigWorkspace is null) return;
        await _workspaces.RemoveMemberAsync(ConfigWorkspace.WorkspaceId, member.UserId);
        await LoadConfigMembersAsync(ConfigWorkspace.WorkspaceId);
        Status = $"{member.DisplayName} removed.";
    }

    [RelayCommand]
    private async Task SaveWorkspaceProvidersAsync()
    {
        if (ConfigWorkspace is null) return;
        var enabled = string.Join(',', ProviderToggles.Where(t => t.IsEnabled).Select(t => t.ProfileId));
        await _wsSettings.SetAsync(ConfigWorkspace.WorkspaceId, WorkspaceSettingsKeys.EnabledProviderProfileIds, enabled);
        Status = "Provider availability saved.";
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task LoadConfigMembersAsync(string workspaceId)
    {
        var members = await _workspaces.ListMembersAsync(workspaceId);
        ConfigWorkspaceMembers.Clear();
        foreach (var m in members)
        {
            var user = await _users.GetAsync(m.UserId);
            ConfigWorkspaceMembers.Add(new ConfigWorkspaceMemberRow(
                UserId: m.UserId,
                DisplayName: user?.Email ?? user?.Username ?? m.UserId,
                Email: user?.Email ?? string.Empty,
                Role: m.Role));
        }
    }

    private async Task LoadProviderTogglesAsync(string workspaceId)
    {
        var allProfiles = await _profileStore.ListAsync(_principal.UserId!);
        var enabledRaw = await _wsSettings.GetAsync(workspaceId, WorkspaceSettingsKeys.EnabledProviderProfileIds);
        var enabledIds = string.IsNullOrEmpty(enabledRaw)
            ? []
            : new HashSet<string>(enabledRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        ProviderToggles.Clear();
        foreach (var p in allProfiles)
            ProviderToggles.Add(new WorkspaceProviderToggleViewModel
            {
                ProfileId = p.ProfileId,
                Name = p.Name,
                ProviderKind = p.ProviderKind,
                IsEnabled = enabledIds.Contains(p.ProfileId),
            });
    }

    private async Task LoadMcpTogglesAsync(string workspaceId)
    {
        var allEntries = await _mcpServerStore.GetAllEntriesAsync();
        var enabledRaw = await _wsSettings.GetAsync(workspaceId, WorkspaceSettingsKeys.EnabledMcpServerIds);
        var enabledIds = string.IsNullOrEmpty(enabledRaw)
            ? []
            : new HashSet<string>(enabledRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        McpToggles.Clear();
        foreach (var e in allEntries.OrderBy(x => x.Name, StringComparer.Ordinal))
            McpToggles.Add(new WorkspaceMcpToggleViewModel { ServerId = e.Id, Name = e.Name, IsEnabled = enabledIds.Contains(e.Id) });
    }

    [RelayCommand]
    private async Task SaveWorkspaceMcpsAsync()
    {
        if (ConfigWorkspace is null) return;
        var enabled = string.Join(',', McpToggles.Where(t => t.IsEnabled).Select(t => t.ServerId));
        await _wsSettings.SetAsync(ConfigWorkspace.WorkspaceId, WorkspaceSettingsKeys.EnabledMcpServerIds, enabled);
        Status = "MCP server availability saved.";
        if (ConfigWorkspace.WorkspaceId == _activeContext.ActiveWorkspaceId)
            await _activeContext.RefreshMcpServersAsync();
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "BaseUrl persisted as TEXT in SQLite.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "BaseUrl persisted as TEXT in SQLite.")]
public sealed record WorkspaceProviderProfile(string ProfileId, string Name, string ProviderKind, string BaseUrl, string CredentialId);

public sealed record ConfigWorkspaceMemberRow(string UserId, string DisplayName, string Email, WorkspaceRole Role);
