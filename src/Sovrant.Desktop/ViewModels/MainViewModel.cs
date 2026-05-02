using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Commands;

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

    partial void OnSelectedGroupChanged(string value)
    {
        OnPropertyChanged(nameof(IsChatGroup));
        OnPropertyChanged(nameof(IsKnowledgeGroup));
        OnPropertyChanged(nameof(IsAgentsGroup));
        OnPropertyChanged(nameof(IsWorkspaceGroup));
        OnPropertyChanged(nameof(IsConnectGroup));
        OnPropertyChanged(nameof(IsGovernanceGroup));
    }

    private readonly IServiceProvider _services;

    public MainViewModel(SidebarViewModel sidebar, CommandPaletteViewModel commandPalette, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(sidebar);
        _sidebar = sidebar;
        _commandPalette = commandPalette;
        _services = services;
        _currentPage = services.GetRequiredService<CommandCenterViewModel>();

        sidebar.NavigationRequested += OnNavigationRequested;
        sidebar.SessionResumeRequested += OnSessionResumeRequested;
        commandPalette.CommandExecuted += OnCommandExecuted;
    }

    [RelayCommand]
    private void SelectGroup(string group)
    {
        SelectedGroup = group;
        // Clicking the chat icon also starts a new chat — replaces the
        // dedicated "New Chat" button and saves the user a click.
        if (group == "chat")
        {
            CurrentPage = CreateChatViewModel();
            Sidebar.SelectedNavItem = "Chat";
        }
    }

    [RelayCommand]
    private void OpenSettings() => OnNavigationRequested(this, "Settings");

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
            "Activity" => _services.GetRequiredService<ActivityViewModel>(),
            "CommandCenter" => _services.GetRequiredService<CommandCenterViewModel>(),
            _ => CurrentPage,
        };
    }

    private async void OnSessionResumeRequested(object? sender, string sessionId)
    {
        var chat = CreateChatViewModel();
        CurrentPage = chat;
        await chat.LoadSessionAsync(sessionId);
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
