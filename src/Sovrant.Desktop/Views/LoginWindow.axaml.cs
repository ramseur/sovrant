using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        // Phase 140: Enter submits: signs in, or on first run creates the administrator.
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    private async void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not LoginViewModel { IsBusy: false } vm)
            return;
        e.Handled = true;
        await vm.SubmitAsync().ConfigureAwait(true);
    }
}
