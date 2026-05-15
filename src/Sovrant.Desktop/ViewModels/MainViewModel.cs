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
        OnPropertyChanged(nameof(IsGovernanceGroup));
        OnPropertyChanged(nameof(IsAdminGroup));
        OnPropertyChanged(nameof(IsSettingsGroup));
    }

    private readonly IServiceProvider _services;
    private readonly IPrincipalAccessor _principal;

    public bool IsAdmin => _principal.IsAdmin;

    public MainViewModel(SidebarViewModel sidebar, CommandPaletteViewModel commandPalette, IServiceProvider services, IPrincipalAccessor principal)
    {
        ArgumentNullException.ThrowIfNull(sidebar);
        _sidebar = sidebar;
        _commandPalette = commandPalette;
        _services = services;
        _principal = principal;
        var cockpit = services.GetRequiredService<CommandCenterViewModel>();
        _currentPage = cockpit;
        cockpit.RowSelected += OnCockpitRowSelected;

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
                var chat = CreateChatViewModel();
                CurrentPage = chat;
                await chat.LoadSessionAsync(e.Id);
                break;
            case "agent-run":
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

    [RelayCommand]
    private void SelectGroup(string group)
    {
        SelectedGroup = group;
        // Clicking a top-level icon should land on the first sub-nav item so the
        // main area is never blank. Chat starts a fresh session; admin guards on role.
        switch (group)
        {
            case "chat":
                CurrentPage = CreateChatViewModel();
                Sidebar.SelectedNavItem = "Chat";
                break;
            case "knowledge":
                OnNavigationRequested(this, "Artifacts");
                break;
            case "agents":
                OnNavigationRequested(this, "Agents");
                break;
            case "workspace":
                OnNavigationRequested(this, "Projects");
                break;
            case "connect":
                OnNavigationRequested(this, "Integrations");
                break;
            case "settings":
                OnNavigationRequested(this, "Settings");
                break;
            case "admin" when _principal.IsAdmin:
                CurrentPage = ResolveAdmin("users");
                break;
        }
    }

    [RelayCommand]
    private static Task LogoutAsync() => App.LogoutAsync();

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

    private void OnNavigationRequested(object? sender, string pageName)
    {
        CurrentPage = pageName switch
        {
            "Chat" => CreateChatViewModel(),
            "Settings" => _services.GetRequiredService<SettingsViewModel>(),
            "Diagnostics" => _services.GetRequiredService<DiagnosticsViewModel>(),
            "Integrations" => _services.GetRequiredService<IntegrationsViewModel>(),
            "Artifacts" => _services.GetRequiredService<ArtifactsViewModel>(),
            "Documents" => _services.GetRequiredService<DocumentsViewModel>(),
            "Tools" => _services.GetRequiredService<ToolsViewModel>(),
            "Skills" => _services.GetRequiredService<SkillsViewModel>(),
            "Memory" => _services.GetRequiredService<MemoryViewModel>(),
            "Governance" => _services.GetRequiredService<GovernanceViewModel>(),
            "TrustBoundary" => _services.GetRequiredService<TrustBoundaryViewModel>(),
            "Projects" => _services.GetRequiredService<ProjectsViewModel>(),
            "Workspaces" => _services.GetRequiredService<WorkspacesViewModel>(),
            "Agents" => _services.GetRequiredService<AgentsViewModel>(),
            "Automations" => _services.GetRequiredService<AutomationsViewModel>(),
            "Orchestration" => _services.GetRequiredService<OrchestrationViewModel>(),
            "CommandCenter" => ResetCockpitToGrid(),
            "Admin" => ResolveAdmin("users"),
            "AdminWorkspaces" => ResolveAdmin("workspaces"),
            _ => CurrentPage,
        };
    }

    private AdminViewModel ResolveAdmin(string section)
    {
        var vm = _services.GetRequiredService<AdminViewModel>();
        vm.Section = section;
        return vm;
    }

    private async void OnSessionResumeRequested(object? sender, string sessionId)
    {
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

    [RelayCommand]
    private void NewChat()
    {
        CurrentPage = CreateChatViewModel();
        Sidebar.SelectedNavItem = "Chat";
    }
}
