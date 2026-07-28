using Avalonia.Controls;
using Avalonia.Input;
using SprintForge.Avalonia.ViewModels;

namespace SprintForge.Avalonia.Views;

public partial class SprintPlanningView : UserControl
{
    private WorkItemRow? _dragItem;
    private double       _dragStartX;
    private int          _dragOriginalStartDay;

    public SprintPlanningView() => InitializeComponent();

    // ── Gantt bar drag (Plan tab + Timeline tab) ──────────────────────────

    private void OnBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border bar || bar.DataContext is not WorkItemRow item) return;
        if (!e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed) return;
        _dragItem             = item;
        _dragStartX           = e.GetCurrentPoint(null).Position.X;
        _dragOriginalStartDay = item.StartDay;
        e.Pointer.Capture(bar);
        e.Handled = true;
    }

    private void OnBarPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragItem is null || sender is not Border bar) return;
        if (!e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed) return;
        double x    = e.GetCurrentPoint(null).Position.X;
        int    delta = (int)Math.Round((x - _dragStartX) / 50.0);
        _dragItem.StartDay = Math.Max(0, Math.Min(14, _dragOriginalStartDay + delta));
    }

    private void OnBarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        _dragItem = null;
    }

    // ── Synchronized scroll (Task Timeline tab) ──────────────────────────

    private void OnTimelineRightScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (Math.Abs(e.OffsetDelta.Y) > 0.01)
            TimelineLeftScroll.Offset = TimelineLeftScroll.Offset.WithY(TimelineRightScroll.Offset.Y);
    }
}
