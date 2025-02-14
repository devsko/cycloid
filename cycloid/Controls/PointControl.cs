using CommunityToolkit.WinUI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public partial class PointControl<T> : UserControl where T : struct, ICanBeInvalid<T>
{
    [GeneratedDependencyProperty]
    public partial T Point { get; set; }

    partial void OnPointPropertyChanged(DependencyPropertyChangedEventArgs e) => PointChanged(e);

    public PointControl()
    {
        Point = default(T).Invalid;
    }

    protected virtual void PointChanged(DependencyPropertyChangedEventArgs e)
    { }
}

public partial class TrackPointControl : PointControl<TrackPoint>
{ }