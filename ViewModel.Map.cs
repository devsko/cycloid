using CommunityToolkit.Mvvm.ComponentModel;
using cycloid.Routing;

namespace cycloid;

partial class ViewModel
{
    [ObservableProperty]
    private bool _trackVisible = true;

    [ObservableProperty]
    private bool _poisVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProfileHoverPointValuesEnabled))]
    private bool _mapHoverPointValuesEnabled;

    private MapPoint? _capturedPoint;

    public bool IsCaptured => _capturedPoint is not null;

    public void StartDrag(MapPoint routePoint)
    {
        Track.RouteBuilder.DelayCalculation = true;
        _capturedPoint = routePoint;
    }

    public void StartDrag(RouteSection section, MapPoint location)
    {
        Track.RouteBuilder.DelayCalculation = true;
        Track.RouteBuilder.InsertPoint(location, section);
        _capturedPoint = location;
    }

    public void ContinueDrag(MapPoint location)
    {
        Track.RouteBuilder.MovePoint(_capturedPoint.Value, location);
        _capturedPoint = location;
    }
    
    public void EndDrag()
    {
        Track.RouteBuilder.DelayCalculation = false;
        _capturedPoint = null;
    }
}