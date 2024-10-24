using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using Windows.UI.Xaml;

namespace cycloid.Controls;

[ObservableObject]
public partial class PointValuesControl<T> : PointControl<T> where T : struct, ICanBeInvalid<T>
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
        DependencyProperty.Register(nameof(Track), typeof(Track), typeof(PointValuesControl<T>), new PropertyMetadata(null));

    public bool Enabled
    {
        get => (bool)GetValue(EnabledProperty);
        set => SetValue(EnabledProperty, value);
    }

    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.Register(nameof(Enabled), typeof(bool), typeof(PointValuesControl<T>), new PropertyMetadata(false, (sender, _) => ((PointValuesControl<T>)sender).EnabledChanged()));

    public bool IsVisible => Enabled && Point.IsValid;

    protected override void PointChanged(DependencyPropertyChangedEventArgs e)
    {
        if (((T)e.OldValue).IsValid != ((T)e.NewValue).IsValid)
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
}

public partial class TrackPointValuesControl : PointValuesControl<TrackPoint>
{
    protected string FilePosition(TrackPoint point) => Track?.FilePosition(point.Distance);

    protected string DistanceFromStart(TrackPoint point) => Format.Distance(Track?.DistanceFromStart(point.Distance));

    protected string TimeFromStart(TrackPoint point) => Format.Duration(Track?.TimeFromStart(point.Time));

    protected string DistanceToEnd(TrackPoint point) => Format.Distance(Track?.DistanceToEnd(point.Distance));

    protected string TimeToEnd(TrackPoint point) => Format.Duration(Track?.TimeToEnd(point.Time));
}

public partial class SelectionValuesControl : PointValuesControl<Selection>
{ }