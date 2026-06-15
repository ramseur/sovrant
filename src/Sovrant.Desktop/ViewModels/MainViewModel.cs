using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Commands;
using Sovrant.Runtime.Auth;

namespace Sovrant.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private SidebarViewModel _sidebar;

    [ObservableProperty]
    private CommandPaletteViewModel _commandPalette;

    /// <summary>Phase 69 — currently selected rail group: chat, knowledge, agents, workspace, connect, governance. Command Center is reached via the Agents panel.</summary>
    [ObservableProperty]
    private string _selectedGroup = "agents";

    public bool IsChatGroup => SelectedGroup == "chat";
    public bool IsKnowledgeGroup => SelectedGroup == "knowledge";
    public bool IsAgentsGroup => SelectedGroup == "agents";
    public bool IsWorkspaceGroup => SelectedGroup == "workspace";
    public bool IsConnectGroup => SelectedGroup == "connect";
    public bool IsDashboardGroup => SelectedGroup == "dashboard";
    public bool IsGovernanceGroup => SelectedGroup == "governance";
    public bool IsAdminGroup => SelectedGroup == "admin";
    public bool IsSettingsGroup => SelectedGroup == "settings";

    partial void OnSelectedGroupChanged(string value)
    {
        OnPropertyChanged(nameof(IsChatGroup));
        OnPropertyChanged(nameof(IsKnowledgeGroup));
        OnPropertyChanged(nameof(IsAgentsGroup));
        OnPropertyChanged(nameof(IsWorkspaceGroup));
        OnPropertyChanged(nameof(IsConnectGroup));
        OnPropertyChanged(nameof(IsDashboardGroup));
        OnPropertyChanged(nameof(IsGovernanceGroup));
        OnPropertyChanged(nameof(IsAdminGroup));
        OnPropertyChanged(nameof(IsSettingsGroup));
    }

    private readonly IServiceProvider _services;
    private readonly IPrincipalAccessor _principal;
    private readonly ActiveSessionsViewModel _activeSessions;
    // Parked ChatViewModels whose turns are still running in background.
    private readonly Dictionary<string, ChatViewModel> _backgroundChats = [];
    private AgentsViewModel? _agentsViewModel;
    private DocumentsViewModel? _documentsViewModel;

    public bool IsAdmin => _principal.IsAdmin;

    [ObservableProperty]
    private bool _isNavCollapsed;

    public bool IsNavExpanded => !IsNavCollapsed;
    public double NavWidth => IsNavCollapsed ? 44 : 240;

    partial void OnIsNavCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNavExpanded));
        OnPropertyChanged(nameof(NavWidth));
    }

    [RelayCommand]
    private void ToggleNav() => IsNavCollapsed = !IsNavCollapsed;

    public MainViewModel(SidebarViewModel sidebar, CommandPaletteViewModel commandPalette, IServiceProvider services, IPrincipalAccessor principal, ActiveSessionsViewModel activeSessions)
    {
        ArgumentNullException.ThrowIfNull(sidebar);
        _sidebar = sidebar;
        _commandPalette = commandPalette;
        _services = services;
        _principal = principal;
        _activeSessions = activeSessions;
        var cockpit = services.GetRequiredService<CommandCenterViewModel>();
        _currentPage = cockpit;
        cockpit.RowSelected += OnCockpitRowSelected;

        var dashboard = services.GetRequiredService<UserDashboardViewModel>();
        dashboard.RowSelected += OnDashboardRowSelected;

        sidebar.NavigationRequested += OnNavigationRequested;
        sidebar.SessionResumeRequested += OnSessionResumeRequested;
        commandPalette.CommandExecuted += OnCommandExecuted;
    }

    /// <summary>
    /// Bridge from the cockpit grid into the matching detail view. Sessions
    /// resume in chat; team runs open Orchestration; agent runs render inline
    /// inside the cockpit (no page swap) so /activity doesn't need to exist.
    /// Missions/claws stay on the cockpit until a dedicated detail view exists.
    /// </summary>
    private async void OnCockpitRowSelected(object? sender, CommandCenterRowSelectedEventArgs e)
    {
        switch (e.Kind)
        {
            case "session":
                if (string.IsNullOrEmpty(e.DetailRoute)) return; // masked — private, not owned by viewer
                var chat = CreateChatViewModel();
                CurrentPage = chat;
                await chat.LoadSessionAsync(e.Id);
                break;
            case "agent-run":
                if (string.IsNullOrEmpty(e.DetailRoute)) return; // masked — private, not owned by viewer
                if (CurrentPage is CommandCenterViewModel cockpit)
                {
                    await cockpit.OpenRunAsync(e.Id);
                }
                break;
            case "team-run":
                CurrentPage = _services.GetRequiredService<OrchestrationViewModel>();
                break;
            // mission/claw — no dedicated detail view yet; keep cockpit visible.
            default:
                break;
        }
    }

    private async void OnDashboardRowSelected(object? sender, CommandCenterRowSelectedEventArgs e)
    {
        switch (e.Kind)
        {
            case "session":
                var chat = CreateChatViewModel();
                CurrentPage = chat;
                SelectedGroup = "chat";
                OnSelectedGroupChanged("chat");
                await chat.LoadSessionAsync(e.Id);
                break;
            case "agent-run":
                var cockpit = _services.GetRequiredService<CommandCenterViewModel>();
                CurrentPage = cockpit;
                SelectedGroup = "dashboard"; // stay on dashboard group visually but show cockpit content
                await cockpit.OpenRunAsync(e.Id);
                break;
            case "team-run":
                CurrentPage = _services.GetRequiredService<OrchestrationViewModel>();
                break;
            // mission/claw — no dedicated detail view yet
            default:
                break;
        }
    }

    [RelayCommand]
    private void SelectGroup(string group)
    {
        SelectedGroup = group;
        // Clicking a top-level icon should land on the first sub-nav item so the
        // main area is never blank. Chat starts a fresh session; admin guards on role.
        ParkCurrentChatIfRunning();
        switch (group)
        {
            case "chat":
                CurrentPage = CreateChatViewModel();
                Sidebar.SelectedNavItem = "Chat";
                break;
            case "knowledge":
                Sidebar.SelectedNavItem = "Artifacts";
                OnNavigationRequested(this, "Artifacts");
                break;
            case "agents":
                Sidebar.SelectedNavItem = "Agents";
                OnNavigationRequested(this, "Agents");
                break;
            case "workspace":
                Sidebar.SelectedNavItem = "Projects";
                OnNavigationRequested(this, "Projects");
                break;
            case "dashboard":
                CurrentPage = _services.GetRequiredService<UserDashboardViewModel>();
                break;
            case "settings":
                Sidebar.SelectedNavItem = "Settings";
                OnNavigationRequested(this, "Settings");
                break;
            case "admin" when _principal.IsAdmin:
                Sidebar.SelectedNavItem = "CommandCenter";
                CurrentPage = ResetCockpitToGrid();
                break;
        }
    }

    [RelayCommand]
    private static Task LogoutAsync() => App.LogoutAsync();

    /// <summary>
    /// Called by App after a new user logs in to flush all stale per-user state.
    /// Resets the nav group, content page, and re-notifies IsAdmin so Avalonia
    /// re-evaluates the admin icon visibility binding.
    /// </summary>
    internal void ResetForUser()
    {
        SelectedGroup = "agents";
        Sidebar.SelectedNavItem = "Agents";
        OnNavigationRequested(this, "Agents");
        OnPropertyChanged(nameof(IsAdmin));
    }

    private void OnCommandExecuted(object? sender, SlashCommandResult result)
    {
        if (CurrentPage is not ChatViewModel chat) return;

        if (result.ShouldClearHistory)
        {
            chat.ClearChatCommand.Execute(null);
            return;
        }

        if (result.InjectAsUserMessage is { } inject)
        {
            chat.InputText = inject;
            if (chat.SendCommand.CanExecute(null))
                chat.SendCommand.Execute(null);
            return;
        }

        if (result.Output is { } output)
        {
            // Show command output as a system message in chat.
            chat.Messages.Add(new MessageViewModel { Role = "assistant", Text = output });
            chat.HasMessages = true;
        }
    }

    private void ParkCurrentChatIfRunning()
    {
        if (CurrentPage is ChatViewModel current
            && current.IsSending
            && _activeSessions.HasSession(current.SessionId))
        {
            _backgroundChats[current.SessionId] = current;
        }
    }

    private void OnNavigationRequested(object? sender, string pageName)
    {
        ParkCurrentChatIfRunning();
        CurrentPage = pageName switch
        {
            "Chat" => CreateChatViewModel(),
            "Settings" => _services.GetRequiredService<SettingsViewModel>(),
            var s when s.StartsWith("Settings:", StringComparison.Ordinal) => ResolveSettings(s),
            "Diagnostics" => _services.GetRequiredService<DiagnosticsViewModel>(),
            "Integrations" => _services.GetRequiredService<IntegrationsViewModel>(),
            "Artifacts" => _services.GetRequiredService<ArtifactsViewModel>(),
            "Documents" => GetOrCreateDocumentsViewModel(),

            "Tools" => _services.GetRequiredService<ToolsViewModel>(),
            "Skills" => _services.GetRequiredService<SkillsViewModel>(),
            "Guidelines" => _services.GetRequiredService<GuidelinesViewModel>(),
            "Memory" => _services.GetRequiredService<MemoryViewModel>(),
            "Governance" => _services.GetRequiredService<GovernanceViewModel>(),
            "TrustBoundary" => _services.GetRequiredService<TrustBoundaryViewModel>(),
            "Projects" => _services.GetRequiredService<ProjectsViewModel>(),
            "Workspaces" => _services.GetRequiredService<WorkspacesViewModel>(),
            "Agents" => GetOrCreateAgentsViewModel(),
            "Automations" => _services.GetRequiredService<AutomationsViewModel>(),
            "Orchestration" => _services.GetRequiredService<OrchestrationViewModel>(),
            "CommandCenter" => ResetCockpitToGrid(),
            "Admin" => ResolveAdmin("users"),
            "AdminWorkspaces" => ResolveAdmin("workspaces"),
            "AdminPlatformIntegrations" => _services.GetRequiredService<IntegrationsViewModel>(),
            "AdminSystemIntegrations" => _services.GetRequiredService<SystemIntegrationsViewModel>(),
            "AdminProviders" => ResolveSettings("Settings:Providers"),
            _ => CurrentPage,
        };
    }

    private SettingsViewModel ResolveSettings(string route)
    {
        var vm = _services.GetRequiredService<SettingsViewModel>();
        var parts = route.Split(':');
        // "Settings:Providers" or "Settings:Providers:OpenAI"
        if (parts.Length >= 2 && string.Equals(parts[1], "Providers", StringComparison.OrdinalIgnoreCase))
        {
            vm.SelectedTab = 1;
            if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
                vm.SelectProvider(parts[2]);
        }
        return vm;
    }

    private AdminViewModel ResolveAdmin(string section)
    {
        var vm = _services.GetRequiredService<AdminViewModel>();
        vm.Section = section;
        return vm;
    }

    private async void OnSessionResumeRequested(object? sender, string sessionId)
    {
        // If the user navigated away while a turn was running, re-attach the parked VM.
        if (_backgroundChats.TryGetValue(sessionId, out var parked))
        {
            _backgroundChats.Remove(sessionId);
            CurrentPage = parked;
            await parked.ReAttachToBackground();
            return;
        }

        var chat = CreateChatViewModel();
        CurrentPage = chat;
        await chat.LoadSessionAsync(sessionId);
    }

    /// <summary>
    /// Returns the singleton cockpit, resetting it out of focused-run mode so
    /// nav-rail re-entry always lands on the grid instead of a stale detail.
    /// </summary>
    private CommandCenterViewModel ResetCockpitToGrid()
    {
        var cockpit = _services.GetRequiredService<CommandCenterViewModel>();
        if (cockpit.BackToGridCommand.CanExecute(null))
            cockpit.BackToGridCommand.Execute(null);
        return cockpit;
    }

    private ChatViewModel CreateChatViewModel()
    {
        var chat = _services.GetRequiredService<ChatViewModel>();
        chat.TurnCompleted += () => _ = Sidebar.RefreshSessionsCommand.ExecuteAsync(null);
        return chat;
    }

    private AgentsViewModel GetOrCreateAgentsViewModel()
    {
        if (_agentsViewModel is not null) return _agentsViewModel;
        _agentsViewModel = ActivatorUtilities.CreateInstance<AgentsViewModel>(
            _services,
            (Action<string, string?>)LaunchAgentChat);
        return _agentsViewModel;
    }

    private DocumentsViewModel GetOrCreateDocumentsViewModel()
    {
        if (_documentsViewModel is not null) return _documentsViewModel;
        _documentsViewModel = ActivatorUtilities.CreateInstance<DocumentsViewModel>(
            _services,
            (Action<string>)LaunchDocumentChat);
        return _documentsViewModel;
    }

    private void LaunchAgentChat(string agentName, string? systemPrompt)
    {
        ParkCurrentChatIfRunning();
        var chat = CreateChatViewModel();
        chat.SetAgentScope(agentName, systemPrompt);
        CurrentPage = chat;
        SelectedGroup = "chat";
        OnSelectedGroupChanged("chat");
        Sidebar.SelectedNavItem = "Chat";
    }

    private void LaunchDocumentChat(string seedText)
    {
        ParkCurrentChatIfRunning();
        var chat = CreateChatViewModel();
        chat.SeedInput(seedText);
        CurrentPage = chat;
        SelectedGroup = "chat";
        OnSelectedGroupChanged("chat");
        Sidebar.SelectedNavItem = "Chat";
    }

    [RelayCommand]
    private void NewChat()
    {
        CurrentPage = CreateChatViewModel();
        Sidebar.SelectedNavItem = "Chat";
    }
}
