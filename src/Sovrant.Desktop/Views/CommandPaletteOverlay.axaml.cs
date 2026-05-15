using Avalonia.Controls;
using Avalonia.Input;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views;

public partial class CommandPaletteOverlay : UserControl
{
    public CommandPaletteOverlay()
    {
        InitializeComponent();
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is CommandPaletteViewModel vm)
            vm.IsOpen = false;
    }

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible)
        {
            SearchInput.Focus();
        }
    }
}
