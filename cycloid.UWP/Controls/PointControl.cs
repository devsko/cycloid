using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public class PointControl<T> : UserControl where T : struct, ICanBeInvalid<T>
{
    public T Point
    {
        get => (T)GetValue(PointProperty);
        set => SetValue(PointProperty, value);
    }

    public static readonly DependencyProperty PointProperty =
        DependencyProperty.Register(nameof(Point), typeof(T), typeof(PointControl<T>), new PropertyMetadata(default(T).Invalid, (sender, e) => ((PointControl<T>)sender).PointChanged(e)));

    protected virtual void PointChanged(DependencyPropertyChangedEventArgs e)
    { }
}

public class TrackPointControl : PointControl<TrackPoint>
{ }