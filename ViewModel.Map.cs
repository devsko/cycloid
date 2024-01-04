using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using cycloid.Routing;
using System;

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

    [ObservableProperty]
    private WayPoint _hoveredWayPoint;

    [ObservableProperty]
    private RouteSection _hoveredSection;

    private WayPoint _capturedPoint;

    public event Action<WayPoint> DragWayPointStarted;

    public bool IsCaptured => _capturedPoint is not null;

    [RelayCommand]
    public void ToggleSectionIsDirectRoute(RouteSection section)
    {
        section.IsDirectRoute = !section.IsDirectRoute;
        Track.RouteBuilder.StartCalculation(section);
    }

    [RelayCommand]
    public void StartDragWayPoint()
    {
        if (Track is not null && !IsCaptured && HoveredWayPoint is not null)
        {
            Track.RouteBuilder.DelayCalculation = true;
            _capturedPoint = HoveredWayPoint;
            DragWayPointStarted?.Invoke(HoveredWayPoint);
        }
    }

    [RelayCommand]
    public void StartDragNewWayPoint(MapPoint location)
    {
        if (Track is not null && !IsCaptured && HoveredSection is not null)
        {
            WayPoint wayPoint = new(location, HoveredSection.IsDirectRoute);
            Track.RouteBuilder.DelayCalculation = true;
            Track.RouteBuilder.InsertPoint(wayPoint, HoveredSection);
            _capturedPoint = wayPoint;
            DragWayPointStarted?.Invoke(wayPoint);
        }
    }

    public void ContinueDragWayPoint(MapPoint location)
    {
        if (Track is not null && IsCaptured)
        {
            _capturedPoint = Track.RouteBuilder.MovePoint(_capturedPoint, location);
        }
    }

    public void EndDragWayPoint()
    {
        if (Track is not null && IsCaptured)
        {
            Track.RouteBuilder.DelayCalculation = false;
            _capturedPoint = null;
        }
    }

    [RelayCommand]
    public void AddDestination(MapPoint location)
    {
        if (Track is not null && !IsCaptured)
        {
            Track.RouteBuilder.AddLastPoint(new WayPoint(location, false));
        }
    }

    [RelayCommand]
    public void DeleteWayPoint()
    {
        if (Track is not null && !IsCaptured && HoveredWayPoint is not null)
        {
            Track.RouteBuilder.RemovePoint(HoveredWayPoint);
        }
    }
}