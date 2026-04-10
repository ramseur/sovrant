using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Sovrant.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private SidebarViewModel _sidebar;

    private readonly IServiceProvider _services;

    public MainViewModel(SidebarViewModel sidebar, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(sidebar);
        _sidebar = sidebar;
        _services = services;
        _currentPage = CreateChatViewModel();

        sidebar.NavigationRequested += OnNavigationRequested;
    }

    private void OnNavigationRequested(object? sender, string pageName)
    {
        CurrentPage = pageName switch
        {
            "Chat" => CreateChatViewModel(),
            "Settings" => (ViewModelBase)Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<SettingsViewModel>(_services),
            _ => CurrentPage,
        };
    }

    private ChatViewModel CreateChatViewModel() =>
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<ChatViewModel>(_services);

    [RelayCommand]
    private void NewChat()
    {
        CurrentPage = CreateChatViewModel();
        Sidebar.SelectedNavItem = "Chat";
    }
}
