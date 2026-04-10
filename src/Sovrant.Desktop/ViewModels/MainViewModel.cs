using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Sovrant.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private SidebarViewModel _sidebar;

    [ObservableProperty]
    private CommandPaletteViewModel _commandPalette;

    private readonly IServiceProvider _services;

    public MainViewModel(SidebarViewModel sidebar, CommandPaletteViewModel commandPalette, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(sidebar);
        _sidebar = sidebar;
        _commandPalette = commandPalette;
        _services = services;
        _currentPage = CreateChatViewModel();

        sidebar.NavigationRequested += OnNavigationRequested;
        sidebar.SessionResumeRequested += OnSessionResumeRequested;
        commandPalette.CommandExecuted += OnCommandExecuted;
    }

    private void OnCommandExecuted(object? sender, string command)
    {
        // Feed the slash command into the current chat as if the user typed it.
        if (CurrentPage is ChatViewModel chat)
        {
            chat.InputText = command;
            if (chat.SendCommand.CanExecute(null))
                chat.SendCommand.Execute(null);
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
            "Projects" => new PlaceholderViewModel("Projects", "📁", "Organize your work into projects with shared context, files, and conversation history."),
            "Workspaces" => new PlaceholderViewModel("Workspaces", "🏢", "Collaborate with your team in shared workspaces with role-based access."),
            "Agents" => new PlaceholderViewModel("Agents", "🤖", "Configure and deploy specialized AI agents for specific tasks and workflows."),
            "Automations" => new PlaceholderViewModel("Automations", "⚡", "Create automated workflows that trigger agents based on events and schedules."),
            "MultiAgent" => new PlaceholderViewModel("Multi-Agent", "👥", "Orchestrate multiple agents working together on complex tasks."),
            _ => CurrentPage,
        };
    }

    private async void OnSessionResumeRequested(object? sender, string sessionId)
    {
        var chat = CreateChatViewModel();
        CurrentPage = chat;
        await chat.LoadSessionAsync(sessionId);
    }

    private ChatViewModel CreateChatViewModel() =>
        _services.GetRequiredService<ChatViewModel>();

    [RelayCommand]
    private void NewChat()
    {
        CurrentPage = CreateChatViewModel();
        Sidebar.SelectedNavItem = "Chat";
    }
}
