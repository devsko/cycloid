using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using cycloid.Routing;

namespace cycloid;

partial class ViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrackIsCalculating))]
    [NotifyPropertyChangedFor(nameof(CompareSessionState))]
    private int _trackCalculationCounter;

    [ObservableProperty]
    private WayPoint _hoveredWayPoint;

    [ObservableProperty]
    private RouteSection _hoveredSection;

    private WayPoint _capturedWayPoint;
    private MapPoint _capturedWayPointOriginalLocation;

    public event Action<WayPoint> DragWayPointStarting;
    public event Action<RouteSection> DragNewWayPointStarting;
    public event Action DragWayPointStarted;
    public event Action DragWayPointEnded;

    public bool TrackIsCalculating => TrackCalculationCounter > 0;

    public bool IsCaptured => _capturedWayPoint is not null;

    [RelayCommand]
    public async Task AddDestinationAsync(MapPoint location)
    {
        if (Mode == Modes.Edit && Track is not null && !IsCaptured)
        {
            await Track.RouteBuilder.AddLastPointAsync(new WayPoint(location, false, false));
        }
    }

    [RelayCommand]
    public async Task AddStartAsync(MapPoint location)
    {
        if (Mode == Modes.Edit && Track is not null && !IsCaptured)
        {
            await Track.RouteBuilder.AddFirstPointAsync(new WayPoint(location, false, false));
        }
    }

    [RelayCommand]
    public async Task AddWayPointAsync(MapPoint location)
    {
        if (Mode == Modes.Edit && Track is not null && !IsCaptured)
        {
            RouteSection nearestSection = null;
            float smallestDistance = float.PositiveInfinity;
            foreach (RouteSection section in Track.RouteBuilder.Sections)
            {
                (_, float distance) = GeoCalculation.MinimalDistance(section.Start.Location, section.End.Location, location);
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
        if (Mode == Modes.Edit && Track is not null && !IsCaptured && HoveredWayPoint is not null)
        {
            await Track.RouteBuilder.RemovePointAsync(HoveredWayPoint);
        }
    }

    [RelayCommand]
    public void StartDragWayPoint()
    {
        if (Mode == Modes.Edit && Track is not null && !IsCaptured && HoveredWayPoint is not null)
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
        if (Mode == Modes.Edit && Track is not null && !IsCaptured && HoveredSection is not null)
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
        if (Mode == Modes.Edit && Track is not null && IsCaptured)
        {
            _capturedWayPoint = await Track.RouteBuilder.MovePointAsync(_capturedWayPoint, location);
        }
    }

    public void EndDragWayPoint()
    {
        if (Mode == Modes.Edit && Track is not null && IsCaptured)
        {
            DragWayPointEnded?.Invoke();
            _capturedWayPoint = null;
            Track.RouteBuilder.DelayCalculation = false;

            SaveTrackAsync().FireAndForget();
        }
    }

    public async Task CancelDragWayPointAsync()
    {
        if (Mode == Modes.Edit && Track is not null && IsCaptured)
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
        if (Mode == Modes.Edit && Track is not null && HoveredWayPoint is not null)
        {
            Track.RouteBuilder.SetFileSplit(HoveredWayPoint, !HoveredWayPoint.IsFileSplit);
        }
    }

    [RelayCommand]
    public void ToggleSectionIsDirectRoute()
    {
        if (Mode == Modes.Edit && Track is not null && HoveredSection is not null)
        {
            Track.RouteBuilder.SetIsDirectRoute(HoveredSection, !HoveredSection.IsDirectRoute);
        }
    }

    private void ConnectRouting(Track track)
    {
        track.RouteBuilder.CalculationStarting -= RouteBuilder_CalculationStarting;
        track.RouteBuilder.CalculationFinished -= RouteBuilder_CalculationFinished;
        track.RouteBuilder.Changed -= RouteBuilder_Changed;
        track.RouteBuilder.FileSplitChanged -= RouteBuilder_FileSplitChanged;
    }

    private void DisconnectRouting(Track track)
    {
        track.RouteBuilder.CalculationStarting += RouteBuilder_CalculationStarting;
        track.RouteBuilder.CalculationFinished += RouteBuilder_CalculationFinished;
        track.RouteBuilder.Changed += RouteBuilder_Changed;
        track.RouteBuilder.FileSplitChanged += RouteBuilder_FileSplitChanged;
    }

    private void RouteBuilder_CalculationStarting(RouteSection _)
    {
        TrackCalculationCounter++;
    }

    private void RouteBuilder_CalculationFinished(RouteSection _1, RouteResult _2)
    {
        TrackCalculationCounter--;
    }

    private void RouteBuilder_Changed(bool initialization)
    {
        if (!initialization && !IsCaptured)
        {
            SaveTrackAsync().FireAndForget();
        }
    }

    private void RouteBuilder_FileSplitChanged(WayPoint wayPoint)
    {
        SaveTrackAsync().FireAndForget();
    }
}
