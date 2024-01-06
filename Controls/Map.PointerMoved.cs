using System;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace cycloid.Controls;

public class PointerPanel : Panel
{
    public PointerPanel()
    {
        Background = new SolidColorBrush(Colors.Transparent);
        Visibility = Visibility.Collapsed;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(1e5, 1e5);
    }
}

partial class Map
{
    private readonly Throttle<PointerRoutedEventArgs, Map> _pointerMovedThrottle = new(
        static (e, @this) => @this.ThrottledClickPanelPointerMoved(e),
        TimeSpan.FromMilliseconds(100));

    private bool PointerMovedEnabled
    {
        get => PointerPanel.Visibility == Visibility.Visible;
        set => PointerPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PointerPanel_Tapped(object _, TappedRoutedEventArgs e)
    {
        ViewModel.EndDragWayPoint(commit: true);
        e.Handled = true;
    }

    private void PointerPanel_PointerMoved(object _, PointerRoutedEventArgs e)
    {
        _pointerMovedThrottle.Next(e, this);
        e.Handled = true;
    }
}
