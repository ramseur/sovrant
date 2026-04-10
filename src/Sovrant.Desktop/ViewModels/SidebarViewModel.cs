using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Sovrant.Desktop.ViewModels;

public partial class SidebarViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _selectedNavItem = "Chat";

    [ObservableProperty]
    private string _connectionStatus = "Connecting...";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _currentModel = "GPT-4o";

    [ObservableProperty]
    private string _currentProvider = "OpenAI";

    [ObservableProperty]
    private bool _isDarkMode = true;

    public event EventHandler<string>? NavigationRequested;

    [RelayCommand]
    private void Navigate(string pageName)
    {
        SelectedNavItem = pageName;
        NavigationRequested?.Invoke(this, pageName);
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant =
                value ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
