using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Sovrant.Desktop.Views;

public partial class UserDashboardView : UserControl
{
    public UserDashboardView() => InitializeComponent();

    // Phase 141 — the "Get started" pill scrolls down to the checklist.
    private void OnGetStartedPill(object? sender, RoutedEventArgs e) =>
        this.FindControl<HomeGuideView>("Guide")?.BringChecklistIntoView();
}
