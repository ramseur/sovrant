using Avalonia;
using Avalonia.Controls;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views;

public partial class IntegrationsView : UserControl
{
    public IntegrationsView()
    {
        InitializeComponent();
    }

    // Phase 139 — follow live MCP connection status only while the page is on screen.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        (DataContext as IntegrationsViewModel)?.StartWatchingStatus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        (DataContext as IntegrationsViewModel)?.StopWatchingStatus();
        base.OnDetachedFromVisualTree(e);
    }
}
