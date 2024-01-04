using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using cycloid.Routing;
using System;
using System.Linq;

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

    private WayPoint _capturedWayPoint;
    private MapPoint _capturedWayPointOriginalLocation;

    public event Action<WayPoint> DragWayPointStarted;
    public event Action DragWayPointEnded;

    public bool IsCaptured => _capturedWayPoint is not null;

    [RelayCommand]
    public void AddDestination(MapPoint location)
    {
        if (Track is not null && !IsCaptured)
        {
            Track.RouteBuilder.AddLastPoint(new WayPoint(location, false));
        }
    }

    [RelayCommand]
    public void AddStart(MapPoint location)
    {
        if (Track is not null && !IsCaptured)
        {
            Track.RouteBuilder.AddFirstPoint(new WayPoint(location, false));
        }
    }

    [RelayCommand]
    public void AddWayPoint(MapPoint location)
    {
        if (Track is not null && !IsCaptured)
        {
            RouteSection nearestSection = null;
            float smallestDistance = float.PositiveInfinity;
            foreach (RouteSection section in Track.RouteBuilder.Sections)
            {
                float distance = GeoCalculation.MinimalDistance(section.Start.Location, section.End.Location, location);
                if (distance < smallestDistance)
                {
                    smallestDistance = distance;
                    nearestSection = section;
                }
            }

            if (nearestSection is not null)
            {
                Track.RouteBuilder.InsertPoint(location, nearestSection);
            }
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

    [RelayCommand]
    public void StartDragWayPoint()
    {
        if (Track is not null && !IsCaptured && HoveredWayPoint is not null)
        {
            Track.RouteBuilder.DelayCalculation = true;
            _capturedWayPoint = HoveredWayPoint;
            _capturedWayPointOriginalLocation = HoveredWayPoint.Location;
            DragWayPointStarted?.Invoke(HoveredWayPoint);
        }
    }

    [RelayCommand]
    public void StartDragNewWayPoint(MapPoint location)
    {
        if (Track is not null && !IsCaptured && HoveredSection is not null)
        {
            Track.RouteBuilder.DelayCalculation = true;
            _capturedWayPoint = Track.RouteBuilder.InsertPoint(location, HoveredSection);
            _capturedWayPointOriginalLocation = MapPoint.Invalid;
            DragWayPointStarted?.Invoke(_capturedWayPoint);
        }
    }

    public void ContinueDragWayPoint(MapPoint location)
    {
        if (Track is not null && IsCaptured)
        {
            _capturedWayPoint = Track.RouteBuilder.MovePoint(_capturedWayPoint, location);
        }
    }

    public void EndDragWayPoint()
    {
        if (Track is not null && IsCaptured)
        {
            Track.RouteBuilder.DelayCalculation = false;
            _capturedWayPoint = null;
            DragWayPointEnded?.Invoke();
        }
    }

    public void CancelDragWayPoint()
    {
        if (Track is not null && IsCaptured)
        {
            if (_capturedWayPointOriginalLocation.IsValid)
            {
                Track.RouteBuilder.MovePoint(_capturedWayPoint, _capturedWayPointOriginalLocation);
            }
            else
            {
                Track.RouteBuilder.RemovePoint(_capturedWayPoint);
            }
            EndDragWayPoint();
        }
    }

    [RelayCommand]
    public void ToggleSectionIsDirectRoute(RouteSection section)
    {
        if (Track is not null)
        {
            section.IsDirectRoute = !section.IsDirectRoute;
            Track.RouteBuilder.StartCalculation(section);
        }
    }
}
