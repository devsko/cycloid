using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

[ObservableObject]
public partial class PointValuesBase : UserControl
{
    private static readonly PropertyChangedEventArgs _isVisibleChangedArgs = new(nameof(IsVisible));

    public Track Track
    {
        get => (Track)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public static readonly DependencyProperty TrackProperty =
    DependencyProperty.Register(nameof(Track), typeof(Track), typeof(PointValuesBase), new PropertyMetadata(null));
    public TrackPoint? Point
    {
        get => (TrackPoint?)GetValue(PointProperty);
        set => SetValue(PointProperty, value);
    }

    public static readonly DependencyProperty PointProperty =
        DependencyProperty.Register(nameof(Point), typeof(TrackPoint?), typeof(PointValuesBase), new PropertyMetadata(null, (sender, e) => ((PointValuesBase)sender).PointChanged(e)));

    public bool Enabled
    {
        get => (bool)GetValue(EnabledProperty);
        set => SetValue(EnabledProperty, value);
    }

    public static DependencyProperty EnabledProperty =
        DependencyProperty.Register(nameof(Enabled), typeof(bool), typeof(PointValuesBase), new PropertyMetadata(false, (sender, _) => ((PointValuesBase)sender).EnabledChanged()));

    public bool IsVisible => Enabled && Point is not null;

    private void PointChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is null != e.NewValue is null)
        {
            RaiseIsVisibleChanged();
        }
    }

    private void EnabledChanged()
    {
        RaiseIsVisibleChanged();
    }

    private void RaiseIsVisibleChanged()
    {
        PropertyChanged?.Invoke(this, _isVisibleChangedArgs);
    }

    protected string FilePosition(TrackPoint point) => Track?.FilePosition(point);

    protected string DistanceFromStart(TrackPoint point) => Track?.DistanceFromStart(point);

    protected string TimeFromStart(TrackPoint point) => Track?.TimeFromStart(point);

    protected string DistanceToEnd(TrackPoint point) => Track?.DistanceToEnd(point);

    protected string TimeToEnd(TrackPoint point) => Track?.TimeToEnd(point);

}