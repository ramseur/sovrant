using Avalonia.Controls;
using Avalonia.Input;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Phase 133 — coming back to the window (e.g. after editing folders on Web)
        // reloads the conversation tree so both surfaces show the same state.
        Activated += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                _ = vm.Sidebar.RefreshFromOtherSurfacesAsync();
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.K && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (DataContext is MainViewModel vm)
            {
                vm.CommandPalette.ToggleCommand.Execute(null);
                e.Handled = true;
            }
        }

        base.OnKeyDown(e);
    }
}
