using System.ComponentModel;
using CommunityToolkit.WinUI;
using Windows.UI.Xaml;

namespace cycloid.Controls;

public partial class PointValuesControl<T> : PointControl<T>, INotifyPropertyChanged where T : struct, ICanBeInvalid<T>
{
    private static readonly PropertyChangedEventArgs _isVisibleChangedArgs = new(nameof(IsVisible));

    public event PropertyChangedEventHandler PropertyChanged;

    public PointValuesControl()
    {
        IsHitTestVisible = false;
    }

    [GeneratedDependencyProperty]
    public partial Track Track { get; set; }

    [GeneratedDependencyProperty]
    public partial bool Enabled { get; set; }

    partial void OnEnabledChanged(bool newValue)
    {
        RaiseIsVisibleChanged();
    }

    public bool IsVisible => Enabled && Point.IsValid;

    protected override void PointChanged(DependencyPropertyChangedEventArgs e)
    {
        if (((T)e.OldValue).IsValid != ((T)e.NewValue).IsValid)
        {
            RaiseIsVisibleChanged();
        }
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
{ 
}