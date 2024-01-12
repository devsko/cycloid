using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using cycloid.Routing;

namespace cycloid;

partial class ViewModel
{
    public const double MinInfoZoomLevel = 12;

    [ObservableProperty]
    private bool _heatmapVisible;

    [ObservableProperty]
    private bool _trackVisible = true;

    [ObservableProperty]
    private bool _poisVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InfoVisible))]
    private bool _infoShouldVisible = true;

    [ObservableProperty]
    private bool _infoIsLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProfileHoverPointValuesEnabled))]
    private bool _mapHoverPointValuesEnabled;

    [ObservableProperty]
    private WayPoint _hoveredWayPoint;

    [ObservableProperty]
    private RouteSection _hoveredSection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InfoEnabled))]
    [NotifyPropertyChangedFor(nameof(InfoVisible))]
    private double _mapZoomLevel;

    private WayPoint _capturedWayPoint;
    private MapPoint _capturedWayPointOriginalLocation;

    public event Action<WayPoint> DragWayPointStarting;
    public event Action<RouteSection> DragNewWayPointStarting;
    public event Action DragWayPointStarted;
    public event Action DragWayPointEnded;

    public bool InfoEnabled => MapZoomLevel >= MinInfoZoomLevel;

    public bool InfoVisible => InfoShouldVisible && InfoEnabled;

    public bool IsCaptured => _capturedWayPoint is not null;

    [RelayCommand]
    public async Task ToggleHeatmapVisibleAsync()
    {
        if (HeatmapVisible)
        {
            HeatmapVisible = false;
        }
        else
        {
            HeatmapVisible = await Strava.InitializeHeatmapAsync(clearCookies: false);
            // Notify property changed again to convinvce the toggle button
            OnPropertyChanged(nameof(HeatmapVisible));
        }
    }

    [RelayCommand]
    public async Task AddDestinationAsync(MapPoint location)
    {
        if (Track is not null && !IsCaptured)
        {
            await Track.RouteBuilder.AddLastPointAsync(new WayPoint(location, false, false));
        }
    }

    [RelayCommand]
    public async Task AddStartAsync(MapPoint location)
    {
        if (Track is not null && !IsCaptured)
        {
            await Track.RouteBuilder.AddFirstPointAsync(new WayPoint(location, false, false));
        }
    }

    [RelayCommand]
    public async Task AddWayPointAsync(MapPoint location)
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
                await Track.RouteBuilder.InsertPointAsync(location, nearestSection);
            }
        }
    }

    [RelayCommand]
    public async Task DeleteWayPointAsync()
    {
        if (Track is not null && !IsCaptured && HoveredWayPoint is not null)
        {
            await Track.RouteBuilder.RemovePointAsync(HoveredWayPoint);
        }
    }

    [RelayCommand]
    public void StartDragWayPoint()
    {
        if (Track is not null && !IsCaptured && HoveredWayPoint is not null)
        {
            Track.RouteBuilder.DelayCalculation = true;
            DragWayPointStarting?.Invoke(HoveredWayPoint);

            _capturedWayPoint = HoveredWayPoint;
            _capturedWayPointOriginalLocation = HoveredWayPoint.Location;
            DragWayPointStarted?.Invoke();
        }
    }

    [RelayCommand]
    public async Task StartDragNewWayPointAsync(MapPoint location)
    {
        if (Track is not null && !IsCaptured && HoveredSection is not null)
        {
            Track.RouteBuilder.DelayCalculation = true;
            DragNewWayPointStarting?.Invoke(HoveredSection);
            
            _capturedWayPoint = await Track.RouteBuilder.InsertPointAsync(location, HoveredSection);
            _capturedWayPointOriginalLocation = MapPoint.Invalid;
            DragWayPointStarted?.Invoke();
        }
    }

    public async Task ContinueDragWayPointAsync(MapPoint location)
    {
        if (Track is not null && IsCaptured)
        {
            _capturedWayPoint = await Track.RouteBuilder.MovePointAsync(_capturedWayPoint, location);
        }
    }

    public void EndDragWayPoint()
    {
        if (Track is not null && IsCaptured)
        {
            DragWayPointEnded?.Invoke();
            _capturedWayPoint = null;
            Track.RouteBuilder.DelayCalculation = false;
            
            SaveTrackAsync().FireAndForget();
        }
    }

    public async Task CancelDragWayPointAsync()
    {
        if (Track is not null && IsCaptured)
        {
            DragWayPointEnded?.Invoke();

            if (_capturedWayPointOriginalLocation.IsValid)
            {
                await Track.RouteBuilder.MovePointAsync(_capturedWayPoint, _capturedWayPointOriginalLocation);
            }
            else
            {
                await Track.RouteBuilder.RemovePointAsync(_capturedWayPoint);
            }

            _capturedWayPoint = null;
            Track.RouteBuilder.DelayCalculation = false;
        }
    }

    [RelayCommand]
    public void TogglePointIsFileSplit()
    {
        if (Track is not null && HoveredWayPoint is not null)
        {
            Track.RouteBuilder.SetFileSplit(HoveredWayPoint, !HoveredWayPoint.IsFileSplit);
        }
    }

    [RelayCommand]
    public void ToggleSectionIsDirectRoute()
    {
        if (Track is not null && HoveredSection is not null)
        {
            Track.RouteBuilder.SetIsDirectRoute(HoveredSection, !HoveredSection.IsDirectRoute);
        }
    }
}
