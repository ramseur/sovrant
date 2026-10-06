using Avalonia.Controls;

namespace Sovrant.Desktop.Views;

public partial class HomeGuideView : UserControl
{
    public HomeGuideView() => InitializeComponent();

    /// <summary>Phase 141 — the header's "Get started" pill scrolls the checklist into view.</summary>
    public void BringChecklistIntoView() => this.FindControl<Control>("GetStartedSection")?.BringIntoView();
}
