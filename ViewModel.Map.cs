using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    private WayPoint _capturedPoint;

    public bool IsCaptured => _capturedPoint is not null;

    [RelayCommand]
    public void ToggleSectionIsDirectRoute(RouteSection section)
    {
        section.IsDirectRoute = !section.IsDirectRoute;
        Track.RouteBuilder.StartCalculation(section);
    }

    public void StartDrag(WayPoint wayPoint)
    {
        if (Track is not null && !IsCaptured)
        {
            Track.RouteBuilder.DelayCalculation = true;
            _capturedPoint = wayPoint;
        }
    }

    public void StartDrag(RouteSection section, WayPoint wayPoint)
    {
        if (Track is not null && !IsCaptured)
        {
            Track.RouteBuilder.DelayCalculation = true;
            Track.RouteBuilder.InsertPoint(wayPoint, section);
            _capturedPoint = wayPoint;
        }
    }

    public void ContinueDrag(MapPoint location)
    {
        if (Track is not null && IsCaptured)
        {
            WayPoint wayPoint = new(location, _capturedPoint.IsDirectRoute);
            Track.RouteBuilder.MovePoint(_capturedPoint, wayPoint);
            _capturedPoint = wayPoint;
        }
    }

    public void EndDrag()
    {
        if (Track is not null && IsCaptured)
        {
            Track.RouteBuilder.DelayCalculation = false;
            _capturedPoint = null;
        }
    }

    public void AddDestination(WayPoint wayPoint)
    {
        if (Track is not null && !IsCaptured)
        {
            Track.RouteBuilder.AddLastPoint(wayPoint);
        }
    }

    public void DeleteRoutePoint(WayPoint wayPoint)
    {
        if (Track is not null && !IsCaptured)
        {
            Track.RouteBuilder.RemovePoint(wayPoint);
        }
    }
}