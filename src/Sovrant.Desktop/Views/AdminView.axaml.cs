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
            vm.ConfirmDisableAsync = (itemType, itemName) =>
                ShowConfirmAsync(itemType, itemName, requireTypeName: false, verb: "Disable");
            vm.ConfirmDeleteAsync = (itemType, itemName) =>
                ShowConfirmAsync(itemType, itemName, requireTypeName: true, verb: "Delete");
        }
    }

    private async Task<bool> ShowConfirmAsync(string itemType, string itemName, bool requireTypeName, string verb)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return false;
        var dialog = new DeleteConfirmDialog(itemType, itemName, requireTypeName, verb);
        return await dialog.ShowDialog<bool>(owner);
    }
}
