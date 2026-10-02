using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views;

/// <summary>
/// Phase 133 — the conversation tree's pointer and drag-and-drop plumbing. Rows are
/// found from the visual tree (their DataContext is a <see cref="SessionTreeRowViewModel"/>);
/// all tree rules and the actual moves live in <see cref="SidebarViewModel"/>.
/// </summary>
public partial class SidebarView : UserControl
{
    /// <summary>Pointer travel (px) before a press on a row turns into a drag instead of a click.</summary>
    private const double DragThreshold = 5;

    /// <summary>Distance (px) from the list's top/bottom edge that auto-scrolls during a drag.</summary>
    private const double AutoScrollZone = 28;

    private PointerPressedEventArgs? _pressArgs;
    private SessionTreeRowViewModel? _pressRow;
    private Point _pressPoint;
    private bool _dragging;

    public SidebarView()
    {
        InitializeComponent();

        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble);

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private SidebarViewModel? Vm => DataContext as SidebarViewModel;

    private static SessionTreeRowViewModel? RowAt(object? source) =>
        (source as Visual)?.GetSelfAndVisualAncestors()
            .OfType<Control>()
            .Select(c => c.DataContext)
            .OfType<SessionTreeRowViewModel>()
            .FirstOrDefault();

    /// <summary>True when <paramref name="source"/> is inside the named heading (TopHead / UnfiledHead).</summary>
    private static bool IsInside(object? source, Control heading) =>
        (source as Visual)?.GetSelfAndVisualAncestors().Contains(heading) == true;

    // ── click vs drag ───────────────────────────────────────────────────────

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Buttons (the ⋯ menu, New folder) handle their own presses.
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        var row = RowAt(e.Source);
        if (row is null)
            return;
        _pressArgs = e;
        _pressRow = row;
        _pressPoint = e.GetPosition(this);
        _dragging = false;
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressArgs is null || _pressRow is null || _dragging || Vm is not { } vm)
            return;
        var delta = e.GetPosition(this) - _pressPoint;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
            return;

        _dragging = true;
        var pressArgs = _pressArgs;
        vm.BeginDrag(_pressRow);
        try
        {
            using var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText("sovrant-conversation-folder-drag"));
            await DragDrop.DoDragDropAsync(pressArgs, data, DragDropEffects.Move);
        }
        finally
        {
            // Covers a drop outside any target or a cancelled drag (Esc).
            if (vm.DragRow is not null)
                vm.EndDrag();
            ResetPress();
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pressRow is { } row && !_dragging && Vm is { } vm && RowAt(e.Source) == row)
            vm.OpenRowCommand.Execute(row);
        ResetPress();
    }

    private void ResetPress()
    {
        _pressArgs = null;
        _pressRow = null;
        _dragging = false;
    }

    // ── drop targets ────────────────────────────────────────────────────────

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (Vm is not { DragRow: not null } vm)
            return;
        if (IsInside(e.Source, TopHead))
            vm.SetHeadDropTarget(top: true);
        else if (IsInside(e.Source, UnfiledHead))
            vm.SetHeadDropTarget(top: false);
        else if (RowAt(e.Source) is { } row)
            vm.SetDropTarget(row.IsFolder ? row : null);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (Vm is not { DragRow: not null } vm)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        AutoScroll(e);

        string? refusal;
        bool isTarget;
        if (IsInside(e.Source, TopHead) || IsInside(e.Source, UnfiledHead))
        {
            isTarget = vm.IsTopDropTarget || vm.IsUnfiledDropTarget;
            refusal = vm.HeadDropRefusal;
        }
        else if (RowAt(e.Source) is { IsFolder: true } row)
        {
            isTarget = true;
            refusal = vm.DropRefusalFor(row.Id);
        }
        else
        {
            isTarget = false;
            refusal = null;
        }
        e.DragEffects = isTarget && refusal is null ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (Vm is not { DragRow: not null } vm)
            return;
        e.Handled = true;
        if (IsInside(e.Source, TopHead) || IsInside(e.Source, UnfiledHead))
            await vm.DropAsync(null, ontoHeading: true);
        else if (RowAt(e.Source) is { IsFolder: true } row)
            await vm.DropAsync(row.Id, ontoHeading: false);
        else
            vm.EndDrag();
    }

    /// <summary>Scrolls the list when a drag nears its top or bottom edge.</summary>
    private void AutoScroll(DragEventArgs e)
    {
        var y = e.GetPosition(TreeScroller).Y;
        var height = TreeScroller.Bounds.Height;
        var offset = TreeScroller.Offset;
        if (y < AutoScrollZone)
            TreeScroller.Offset = offset.WithY(Math.Max(0, offset.Y - 12));
        else if (y > height - AutoScrollZone)
            TreeScroller.Offset = offset.WithY(offset.Y + 12);
    }
}
