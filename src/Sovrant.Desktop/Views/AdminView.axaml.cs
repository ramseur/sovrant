using Avalonia.Controls;
using Avalonia.Interactivity;
using Sovrant.Desktop.ViewModels;
using Sovrant.Desktop.Views.Dialogs;

namespace Sovrant.Desktop.Views;

public partial class AdminView : UserControl
{
    public AdminView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is AdminViewModel vm)
        {
            _ = vm.LoadAsync();
            vm.ConfirmDeleteAsync = ShowDeleteConfirmAsync;
        }
    }

    private async Task<bool> ShowDeleteConfirmAsync(string itemType, string itemName)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return false;
        var dialog = new DeleteConfirmDialog(itemType, itemName, requireTypeName: false);
        return await dialog.ShowDialog<bool>(owner);
    }
}
