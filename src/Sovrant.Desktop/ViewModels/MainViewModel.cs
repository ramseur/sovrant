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
            "Tools" => _services.GetRequiredService<ToolsViewModel>(),
            "Memory" => _services.GetRequiredService<MemoryViewModel>(),
            "Governance" => _services.GetRequiredService<GovernanceViewModel>(),
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
