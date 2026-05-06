using Avalonia.Controls;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views;

public partial class AdminView : UserControl
{
    public AdminView()
    {
        InitializeComponent();
    }

    protected override async void OnInitialized()
    {
        base.OnInitialized();
        if (DataContext is AdminViewModel vm)
            await vm.LoadAsync().ConfigureAwait(true);
    }
}
