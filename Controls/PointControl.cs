using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public class PointControl : UserControl
{
    public TrackPoint Point
    {
        get => (TrackPoint)GetValue(PointProperty);
        set => SetValue(PointProperty, value);
    }

    public static readonly DependencyProperty PointProperty =
        DependencyProperty.Register(nameof(Point), typeof(TrackPoint), typeof(PointControl), new PropertyMetadata(TrackPoint.Invalid, (sender, e) => ((PointControl)sender).PointChanged(e)));

    protected virtual void PointChanged(DependencyPropertyChangedEventArgs e)
    { }
}