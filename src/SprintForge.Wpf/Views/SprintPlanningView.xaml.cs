using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SprintForge.Wpf.ViewModels;

namespace SprintForge.Wpf.Views;

public partial class SprintPlanningView : UserControl
{
    private WorkItemRow? _dragItem;
    private double       _dragStartX;
    private int          _dragOriginalStartDay;

    public SprintPlanningView() { InitializeComponent(); }

    // ── Gantt bar drag (Plan tab + Timeline tab) ──────────────────────────

    private void OnBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement bar || bar.DataContext is not WorkItemRow item) return;
        if (bar.Parent is not Canvas canvas) return;
        _dragItem            = item;
        _dragStartX          = e.GetPosition(canvas).X;
        _dragOriginalStartDay = item.StartDay;
        bar.CaptureMouse();
        e.Handled = true;
    }

    private void OnBarMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem == null || e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not FrameworkElement bar || bar.Parent is not Canvas canvas) return;
        double x   = e.GetPosition(canvas).X;
        int    delta = (int)Math.Round((x - _dragStartX) / 50.0);
        _dragItem.StartDay = Math.Max(0, Math.Min(14, _dragOriginalStartDay + delta));
    }

    private void OnBarMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement bar) bar.ReleaseMouseCapture();
        _dragItem = null;
    }

    // ── Synchronized scroll (Task Timeline tab) ──────────────────────────

    private void OnTimelineRightScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (Math.Abs(e.VerticalChange) > 0.01)
            TimelineLeftScroll.ScrollToVerticalOffset(e.VerticalOffset);
    }
}
