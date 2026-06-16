using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Users;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Desktop.ViewModels;

public partial class WorkspacesViewModel : ViewModelBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IUserService _userService;
    private readonly ActiveContextViewModel _activeContext;
    private readonly IPrincipalAccessor _principal;
    private readonly IProviderProfileStore _profileStore;
    private readonly IWorkspaceSettingsStore _wsSettings;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _newWorkspaceName = string.Empty;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private WorkspaceItemViewModel? _selectedWorkspace;
    [ObservableProperty] private string _detailMarkdown = string.Empty;

    // Member panel
    [ObservableProperty] private bool _membersLoading;
    [ObservableProperty] private string _addMemberUserId = string.Empty;
    [ObservableProperty] private WorkspaceRole _addMemberRole = WorkspaceRole.Member;

    // Provider panel
    [ObservableProperty] private bool _providersLoading;

    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];
    public ObservableCollection<WorkspaceProviderToggleViewModel> ProviderToggles { get; } = [];

    /// <summary>Set by the View to show a confirmation dialog before destructive actions.</summary>
    public Func<string, string, Task<bool>>? ConfirmDeleteAsync { get; set; }
    public ObservableCollection<WorkspaceMemberViewModel> Members { get; } = [];
    public IReadOnlyList<WorkspaceRole> RoleOptions { get; } = [WorkspaceRole.Member, WorkspaceRole.Admin, WorkspaceRole.Viewer];

    public bool IsAdmin => _principal.Role == "admin";

    private static string DesktopUserId => App.SovrantUserId;

    public WorkspacesViewModel(IWorkspaceService workspaceService, IUserService userService,
        ActiveContextViewModel activeContext, IPrincipalAccessor principal,
        IProviderProfileStore profileStore, IWorkspaceSettingsStore wsSettings)
    {
        _workspaceService = workspaceService;
        _userService = userService;
        _activeContext = activeContext;
        _principal = principal;
        _profileStore = profileStore;
        _wsSettings = wsSettings;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task SelectWorkspaceAsync(WorkspaceItemViewModel workspace)
    {
        SelectedWorkspace = workspace;
        await LoadMembersAsync(workspace);
        if (IsAdmin) await LoadProvidersAsync(workspace);
    }

    [RelayCommand]
    private async Task CreateWorkspaceAsync()
    {
        var name = NewWorkspaceName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        try
        {
#pragma warning disable CA1308
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

        if (ConfirmDeleteAsync is not null && !await ConfirmDeleteAsync("Workspace", workspace.Name)) return;

        var success = await _workspaceService.DeleteAsync(workspace.Id);
        if (success)
        {
            if (SelectedWorkspace == workspace) { SelectedWorkspace = null; Members.Clear(); }
            StatusMessage = $"Deleted workspace '{workspace.Name}'.";
            await LoadAsync();
            await _activeContext.RefreshCommand.ExecuteAsync(null);
        }
        else
        {
            StatusMessage = "Failed to delete workspace.";
        }
    }

    [RelayCommand]
    private async Task AddMemberAsync()
    {
        if (SelectedWorkspace is null) return;
        var input = AddMemberUserId.Trim();
        if (string.IsNullOrEmpty(input)) { StatusMessage = "Enter an email or username."; return; }

        try
        {
            var user = await _userService.GetByEmailAsync(input)
                       ?? await _userService.GetByUsernameAsync(input)
                       ?? await _userService.GetAsync(input);
            if (user is null) { StatusMessage = $"User '{input}' not found."; return; }

            await _workspaceService.AddMemberAsync(SelectedWorkspace.Id, user.UserId, AddMemberRole);
            AddMemberUserId = string.Empty;
            StatusMessage = $"Added {user.Email ?? user.Username} as {AddMemberRole}.";
            await LoadMembersAsync(SelectedWorkspace);
        }
        catch (Exception ex) { StatusMessage = $"Failed to add member: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task RemoveMemberAsync(WorkspaceMemberViewModel member)
    {
        if (SelectedWorkspace is null) return;
        try
        {
            await _workspaceService.RemoveMemberAsync(SelectedWorkspace.Id, member.UserId);
            StatusMessage = $"Removed {member.DisplayName}.";
            await LoadMembersAsync(SelectedWorkspace);
        }
        catch (Exception ex) { StatusMessage = $"Failed to remove member: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task SaveProviderSettingsAsync()
    {
        if (SelectedWorkspace is null) return;
        var enabled = ProviderToggles.Where(t => t.IsEnabled).Select(t => t.ProfileId);
        await _wsSettings.SetAsync(SelectedWorkspace.Id, WorkspaceSettingsKeys.EnabledProviderProfileIds,
            string.Join(',', enabled));
        StatusMessage = "Provider availability saved.";
    }

    private async Task LoadProvidersAsync(WorkspaceItemViewModel workspace)
    {
        ProvidersLoading = true;
        try
        {
            var allProfiles = await _profileStore.ListAsync(DesktopUserId);
            var enabledRaw = await _wsSettings.GetAsync(workspace.Id, WorkspaceSettingsKeys.EnabledProviderProfileIds);
            var enabledIds = string.IsNullOrEmpty(enabledRaw)
                ? []
                : new HashSet<string>(enabledRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProviderToggles.Clear();
                foreach (var p in allProfiles)
                    ProviderToggles.Add(new WorkspaceProviderToggleViewModel
                    {
                        ProfileId = p.ProfileId,
                        Name = p.Name,
                        ProviderKind = p.ProviderKind,
                        IsEnabled = enabledIds.Contains(p.ProfileId),
                    });
            });
        }
        catch (Exception ex) { StatusMessage = $"Could not load providers: {ex.Message}"; }
        finally { ProvidersLoading = false; }
    }

    partial void OnSelectedWorkspaceChanged(WorkspaceItemViewModel? value)
    {
        if (value is null) { Members.Clear(); ProviderToggles.Clear(); return; }
        DetailMarkdown = BuildWorkspaceMarkdown(value);
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
            var roleMap = new Dictionary<string, WorkspaceRole?>();
            foreach (var ws in workspaces)
                roleMap[ws.WorkspaceId] = await _workspaceService.GetMemberRoleAsync(ws.WorkspaceId, DesktopUserId);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Workspaces.Clear();
                foreach (var w in workspaces)
                {
                    var myRole = roleMap.TryGetValue(w.WorkspaceId, out var r) ? r : null;
                    var item = new WorkspaceItemViewModel
                    {
                        Id = w.WorkspaceId,
                        Name = w.Name,
                        Slug = w.Slug,
                        IsPersonal = w.Type == WorkspaceType.Personal,
                        CreatedAt = w.CreatedAt,
                        MyRole = myRole,
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

    private async Task LoadMembersAsync(WorkspaceItemViewModel workspace)
    {
        MembersLoading = true;
        try
        {
            var list = await _workspaceService.ListMembersAsync(workspace.Id);
            var rows = new List<WorkspaceMemberViewModel>(list.Count);
            foreach (var m in list)
            {
                var user = await _userService.GetAsync(m.UserId);
                rows.Add(new WorkspaceMemberViewModel
                {
                    UserId = m.UserId,
                    Email = user?.Email ?? string.Empty,
                    Username = user?.Username ?? string.Empty,
                    DisplayName = user?.Email ?? user?.Username ?? m.UserId,
                    Role = m.Role,
                    JoinedAt = m.JoinedAt,
                });
            }
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Members.Clear();
                foreach (var row in rows) Members.Add(row);
            });
        }
        catch (Exception ex) { StatusMessage = $"Could not load members: {ex.Message}"; }
        finally { MembersLoading = false; }
    }

    public bool CanManage(WorkspaceItemViewModel workspace) =>
        IsAdmin || workspace.MyRole is WorkspaceRole.Owner or WorkspaceRole.Admin;

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
        if (workspace.MyRole is not null)
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Your role:** {workspace.MyRole}");
        }
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
    [ObservableProperty] private WorkspaceRole? _myRole;
    public string Markdown { get; set; } = string.Empty;
}

public partial class WorkspaceMemberViewModel : ViewModelBase
{
    [ObservableProperty] private string _userId = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private WorkspaceRole _role;
    [ObservableProperty] private DateTimeOffset _joinedAt;
}

public partial class WorkspaceProviderToggleViewModel : ViewModelBase
{
    [ObservableProperty] private string _profileId = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _providerKind = string.Empty;
    [ObservableProperty] private bool _isEnabled;
}
