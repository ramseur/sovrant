using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views;

public partial class MainWindow : Window
{
    /// <summary>Phase 135 — Conversations never get less than this in the expanded rail.</summary>
    private const double MinConversationsHeight = 160;

    private Control? _openFlyoutOwner;

    public MainWindow()
    {
        InitializeComponent();

        // Phase 133 — coming back to the window (e.g. after editing folders on Web)
        // reloads the conversation tree so both surfaces show the same state.
        Activated += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                _ = vm.Sidebar.RefreshFromOtherSurfacesAsync();
        };

        // Phase 135 — option 2 sizing: the grid fills the visible rail (so Conversations take
        // all the room the nav leaves), but never shrinks below nav + 160px of Conversations;
        // only then does the surrounding ScrollViewer scroll the rail's middle as one.
        RailScroll.SizeChanged += (_, _) => SizeRailGrid();
        NavRows.SizeChanged += (_, _) => SizeRailGrid();
    }

    private void SizeRailGrid()
    {
        var visible = RailScroll.Bounds.Height;
        if (visible <= 0)
            return;
        // nav rows + their margin (12) + divider (1) + "CONVERSATIONS" label (~24)
        var needed = NavRows.Bounds.Height + 12 + 1 + 24 + MinConversationsHeight;
        RailGrid.Height = Math.Max(visible, needed);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.K && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (DataContext is MainViewModel vm)
            {
                vm.CommandPalette.ToggleCommand.Execute(null);
                e.Handled = true;
            }
        }

        base.OnKeyDown(e);
    }

    // ── Phase 135 — collapsed-rail flyouts ──────────────────────────────────
    // Click (or Enter/Space, which raise Click) and hover both open the flyout;
    // Esc and clicking outside close it (Flyout light-dismiss). Groups without
    // pages (Dashboard, Projects) just navigate.

    private void OnRailIconPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control c && c.DataContext is AppNavRowViewModel { HasFlyout: true })
            ShowFlyout(c);
    }

    private void OnRailIconClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.DataContext is not AppNavRowViewModel row)
            return;
        if (row.HasFlyout)
            ShowFlyout(c);
        else if (DataContext is MainViewModel vm)
            vm.AppNav.ActivateCommand.Execute(row);
    }

    private void ShowFlyout(Control owner)
    {
        if (ReferenceEquals(_openFlyoutOwner, owner))
            return;
        HideFlyout();
        if (owner.DataContext is AppNavRowViewModel { IsChatGroup: true } && DataContext is MainViewModel vm)
            _ = vm.Sidebar.RefreshFromOtherSurfacesAsync(); // fresh "recent conversations"
        FlyoutBase.ShowAttachedFlyout(owner);
        _openFlyoutOwner = owner;
        if (FlyoutBase.GetAttachedFlyout(owner) is { } flyout)
            flyout.Closed += (_, _) => { if (ReferenceEquals(_openFlyoutOwner, owner)) _openFlyoutOwner = null; };
    }

    private void HideFlyout()
    {
        if (_openFlyoutOwner is not null)
            FlyoutBase.GetAttachedFlyout(_openFlyoutOwner)?.Hide();
        _openFlyoutOwner = null;
    }

    private void OnFlyoutItemClick(object? sender, RoutedEventArgs e) => HideFlyout();

    private void OnShowAllConversationsClick(object? sender, RoutedEventArgs e)
    {
        HideFlyout();
        if (DataContext is MainViewModel vm && vm.IsNavCollapsed)
            vm.ToggleNavCommand.Execute(null);
    }
}
