using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using Windows.UI.Xaml;

namespace cycloid.Controls;

[ObservableObject]
public partial class PointValuesControl : PointControl
{
    private static readonly PropertyChangedEventArgs _isVisibleChangedArgs = new(nameof(IsVisible));

    public PointValuesControl()
    {
        IsHitTestVisible = false;
    }

    public Track Track
    {
        get => (Track)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public static readonly DependencyProperty TrackProperty =
    DependencyProperty.Register(nameof(Track), typeof(Track), typeof(PointValuesControl), new PropertyMetadata(null));

    public bool Enabled
    {
        get => (bool)GetValue(EnabledProperty);
        set => SetValue(EnabledProperty, value);
    }

    public static DependencyProperty EnabledProperty =
        DependencyProperty.Register(nameof(Enabled), typeof(bool), typeof(PointValuesControl), new PropertyMetadata(false, (sender, _) => ((PointValuesControl)sender).EnabledChanged()));

    public bool IsVisible => Enabled && Point.IsValid;

    protected override void PointChanged(DependencyPropertyChangedEventArgs e)
    {
        if (((TrackPoint)e.OldValue).IsValid != ((TrackPoint)e.NewValue).IsValid)
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

    protected string FilePosition(TrackPoint point) => Track?.FilePosition(point.Distance);

    protected string DistanceFromStart(TrackPoint point) => Track?.DistanceFromStart(point.Distance);

    protected string TimeFromStart(TrackPoint point) => Track?.TimeFromStart(point.Time);

    protected string DistanceToEnd(TrackPoint point) => Track?.DistanceToEnd(point.Distance);

    protected string TimeToEnd(TrackPoint point) => Track?.TimeToEnd(point.Time);
}