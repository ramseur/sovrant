using Avalonia.Controls;
using Sovrant.Desktop.ViewModels;

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
            _ = vm.LoadAsync();
    }
}
