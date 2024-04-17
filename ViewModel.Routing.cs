using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using cycloid.Routing;

namespace cycloid;

public class DragWayPointStarting(WayPoint wayPoint)
{
    public WayPoint WayPoint => wayPoint;
}

public class DragNewWayPointStarting(RouteSection section)
{
    public RouteSection Section => section;
}

public class DragWayPointStarted();

public class DragWayPointEnded();

partial class ViewModel :
    IRecipient<CalculationStarting>,
    IRecipient<CalculationFinished>,
    IRecipient<RouteChanged>,
    IRecipient<FileSplitChanged>
{
    private int _trackCalculationCounter;
    private WayPoint _hoveredWayPoint;
    private RouteSection _hoveredSection;
    private WayPoint _capturedWayPoint;
    private MapPoint _capturedWayPointOriginalLocation;

    public int TrackCalculationCounter
    {
        get => _trackCalculationCounter;
        set
        {
            if (SetProperty(ref _trackCalculationCounter, value))
            {
                OnPropertyChanged(nameof(TrackIsCalculating));
                OnPropertyChanged(nameof(CompareSessionState));
            }
        }
    }

    public WayPoint HoveredWayPoint
    {
        get => _hoveredWayPoint;
        set
        {
            if (SetProperty(ref _hoveredWayPoint, value))
            {
                if (value is null)
                {
                    HoverPoint = TrackPoint.Invalid;
                }
                else
                {
                    (RouteSection to, RouteSection from) = Track.RouteBuilder.GetSections(value);
                    if (from is null)
                    {
                        HoverPoint = Track.Points.Last();
                    }
                    else
                    {
                        HoverPoint = Track.Points[new Track.Index(Track.RouteBuilder.GetSectionIndex(from), 0)];
                    }
                }
            }
        }
    }

    public RouteSection HoveredSection
    {
        get => _hoveredSection;
        set
        {
            if (SetProperty(ref _hoveredSection, value))
            {
                if (value is null)
                {
                    HoverPoint = TrackPoint.Invalid;
                }
                else
                {
                    int index = Track.RouteBuilder.GetSectionIndex(value);
                    int pointCount = Track.Points.GetPointCount(index);

                    HoverPoint = Track.Points[new Track.Index(index, pointCount / 2)];
                }
            }
        }
    }

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

            StrongReferenceMessenger.Default.Send(new DragWayPointStarting(HoveredWayPoint));

            _capturedWayPoint = HoveredWayPoint;
            _capturedWayPointOriginalLocation = HoveredWayPoint.Location;

            StrongReferenceMessenger.Default.Send(new DragWayPointStarted());
        }
    }

    [RelayCommand]
    public async Task StartDragNewWayPointAsync(MapPoint location)
    {
        if (Mode == Modes.Edit && Track is not null && !IsCaptured && HoveredSection is not null)
        {
            Track.RouteBuilder.DelayCalculation = true;

            StrongReferenceMessenger.Default.Send(new DragNewWayPointStarting(HoveredSection));

            _capturedWayPoint = await Track.RouteBuilder.InsertPointAsync(location, HoveredSection);
            _capturedWayPointOriginalLocation = MapPoint.Invalid;

            StrongReferenceMessenger.Default.Send(new DragWayPointStarted());
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
            StrongReferenceMessenger.Default.Send(new DragWayPointEnded());

            _capturedWayPoint = null;
            Track.RouteBuilder.DelayCalculation = false;

            SaveTrackAsync().FireAndForget();
        }
    }

    public async Task CancelDragWayPointAsync()
    {
        if (Mode == Modes.Edit && Track is not null && IsCaptured)
        {
            StrongReferenceMessenger.Default.Send(new DragWayPointEnded());

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
        if (track is not null)
        {
            StrongReferenceMessenger.Default.Register<CalculationStarting>(this);
            StrongReferenceMessenger.Default.Register<CalculationFinished>(this);
            StrongReferenceMessenger.Default.Register<RouteChanged>(this);
            StrongReferenceMessenger.Default.Register<FileSplitChanged>(this);
        }
    }

    private void DisconnectRouting(Track track)
    {
        if (track is not null)
        {
            StrongReferenceMessenger.Default.Unregister<CalculationStarting>(this);
            StrongReferenceMessenger.Default.Unregister<CalculationFinished>(this);
            StrongReferenceMessenger.Default.Unregister<RouteChanged>(this);
            StrongReferenceMessenger.Default.Unregister<FileSplitChanged>(this);
        }
    }

    void IRecipient<CalculationStarting>.Receive(CalculationStarting message)
    {
        TrackCalculationCounter++;
    }

    void IRecipient<CalculationFinished>.Receive(CalculationFinished message)
    {
        TrackCalculationCounter--;
    }

    void IRecipient<RouteChanged>.Receive(RouteChanged message)
    {
        if (!message.Initialization && !IsCaptured)
        {
            SaveTrackAsync().FireAndForget();
        }
    }

    void IRecipient<FileSplitChanged>.Receive(FileSplitChanged message)
    {
        SaveTrackAsync().FireAndForget();
    }
}
