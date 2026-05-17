using Avalonia.Controls;
using Avalonia.Interactivity;
using Sovrant.Desktop.ViewModels;
using Sovrant.Desktop.Views.Dialogs;

namespace Sovrant.Desktop.Views;

public partial class WorkspacesView : UserControl
{
    public WorkspacesView() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is WorkspacesViewModel vm)
            vm.ConfirmDeleteAsync = ShowDeleteConfirmAsync;
    }

    private async Task<bool> ShowDeleteConfirmAsync(string itemType, string itemName)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return false;
        var dialog = new DeleteConfirmDialog(itemType, itemName);
        return await dialog.ShowDialog<bool>(owner);
    }
}
