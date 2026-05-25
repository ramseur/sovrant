using Avalonia.Controls;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views;

public partial class SystemIntegrationsView : UserControl
{
    public SystemIntegrationsView()
    {
        InitializeComponent();
    }

    protected override async void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SystemIntegrationsViewModel vm)
            await vm.LoadAsync();
    }
}
