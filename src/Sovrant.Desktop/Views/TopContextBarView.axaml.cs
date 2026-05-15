using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Sovrant.Desktop.Views;

public partial class TopContextBarView : UserControl
{
    public TopContextBarView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
