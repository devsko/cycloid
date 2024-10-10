using System;
using System.Linq;
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
    IRecipient<SectionAdded>,
    IRecipient<SectionRemoved>,
    IRecipient<CalculationStarting>,
    IRecipient<CalculationFinished>,
    IRecipient<RouteChanging>,
    IRecipient<RouteChanged>,
    IRecipient<FileSplitChanged>
{
    private WayPoint _hoveredWayPoint;
    private RouteSection _hoveredSection;
    private WayPoint _capturedWayPoint;
    private MapPoint _capturedWayPointOriginalLocation;

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
                else if (!Track.RouteBuilder.IsCalculating)
                {
                    (RouteSection to, RouteSection from) = Track.RouteBuilder.GetSections(value);
                    if (from is null)
                    {
                        HoverPoint = to is null ? TrackPoint.Invalid : Track.Points.Last();
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
                else if (!Track.RouteBuilder.IsCalculating)
                {
                    int index = Track.RouteBuilder.GetSectionIndex(value);
                    int pointCount = Track.Points.GetPointCount(index);

                    HoverPoint = Track.Points[new Track.Index(index, pointCount / 2)];
                }
            }
        }
    }

    public bool TrackIsCalculating => Track is not null && Track.RouteBuilder.IsCalculating;

    public bool IsDraggingWayPoint => _capturedWayPoint is not null;

    [RelayCommand]
    public async Task AddDestinationAsync(MapPoint location)
    {
        if (IsEditMode && Track is not null && !IsDraggingWayPoint)
        {
            await Track.RouteBuilder.AddLastPointAsync(new WayPoint(location, false, false));
        }
    }

    [RelayCommand]
    public async Task AddStartAsync(MapPoint location)
    {
        if (IsEditMode && Track is not null && !IsDraggingWayPoint)
        {
            await Track.RouteBuilder.AddFirstPointAsync(new WayPoint(location, false, false));
        }
    }

    [RelayCommand]
    public async Task AddWayPointAsync(MapPoint location)
    {
        if (IsEditMode && Track is not null && !IsDraggingWayPoint)
        {
            if (Track.Points.IsEmpty)
            {
                await Track.RouteBuilder.AddLastPointAsync(new WayPoint(location, false, false));
            }
            else
            {
                RouteSection nearestSection = Track.RouteBuilder.Sections
                    .MinBy(section => GeoCalculation.MinimalDistance(section.Start.Location, section.End.Location, location).Distance);
                
                await Track.RouteBuilder.InsertPointAsync(location, nearestSection);
            }
        }
    }

    [RelayCommand]
    public async Task DeleteWayPointAsync()
    {
        if (IsEditMode && Track is not null && !IsDraggingWayPoint && HoveredWayPoint is not null)
        {
            await Track.RouteBuilder.RemovePointAsync(HoveredWayPoint);
        }
    }

    [RelayCommand]
    public void StartDragWayPoint()
    {
        if (IsEditMode && Track is not null && !IsDraggingWayPoint && HoveredWayPoint is not null)
        {
            Track.RouteBuilder.DelayCalculation = CalculationDelayMode.LongSections;

            StrongReferenceMessenger.Default.Send(new DragWayPointStarting(HoveredWayPoint));

            _capturedWayPoint = HoveredWayPoint;
            _capturedWayPointOriginalLocation = HoveredWayPoint.Location;

            StrongReferenceMessenger.Default.Send(new DragWayPointStarted());
        }
    }

    [RelayCommand]
    public async Task StartDragNewWayPointAsync(MapPoint location)
    {
        if (IsEditMode && Track is not null && !IsDraggingWayPoint && HoveredSection is not null)
        {
            Track.RouteBuilder.DelayCalculation = CalculationDelayMode.LongSections;

            StrongReferenceMessenger.Default.Send(new DragNewWayPointStarting(HoveredSection));

            _capturedWayPoint = await Track.RouteBuilder.InsertPointAsync(location, HoveredSection);
            _capturedWayPointOriginalLocation = MapPoint.Invalid;

            StrongReferenceMessenger.Default.Send(new DragWayPointStarted());
        }
    }

    public async Task ContinueDragWayPointAsync(MapPoint location)
    {
        if (IsEditMode && Track is not null && IsDraggingWayPoint)
        {
            _capturedWayPoint = await Track.RouteBuilder.MovePointAsync(_capturedWayPoint, location);
        }
    }

    public void EndDragWayPoint()
    {
        if (IsEditMode && Track is not null && IsDraggingWayPoint)
        {
            StrongReferenceMessenger.Default.Send(new DragWayPointEnded());

            _capturedWayPoint = null;
            Track.RouteBuilder.DelayCalculation = CalculationDelayMode.None;

            SaveTrackAsync().FireAndForget();
        }
    }

    public async Task CancelDragWayPointAsync()
    {
        if (IsEditMode && Track is not null && IsDraggingWayPoint)
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
            Track.RouteBuilder.DelayCalculation = CalculationDelayMode.None;
        }
    }

    [RelayCommand]
    public void TogglePointIsFileSplit()
    {
        if (IsEditMode && Track is not null && HoveredWayPoint is not null)
        {
            Track.RouteBuilder.SetFileSplit(HoveredWayPoint, !HoveredWayPoint.IsFileSplit);
        }
    }

    [RelayCommand]
    public void ToggleSectionIsDirectRoute()
    {
        if (IsEditMode && Track is not null && HoveredSection is not null)
        {
            Track.RouteBuilder.SetIsDirectRoute(HoveredSection, !HoveredSection.IsDirectRoute);
        }
    }

    private void ConnectRouting(Track track)
    {
        if (track is not null)
        {
            StrongReferenceMessenger.Default.Register<SectionAdded>(this);
            StrongReferenceMessenger.Default.Register<SectionRemoved>(this);
            StrongReferenceMessenger.Default.Register<CalculationStarting>(this);
            StrongReferenceMessenger.Default.Register<CalculationFinished>(this);
            StrongReferenceMessenger.Default.Register<RouteChanging>(this);
            StrongReferenceMessenger.Default.Register<RouteChanged>(this);
            StrongReferenceMessenger.Default.Register<FileSplitChanged>(this);
        }
    }

    private void DisconnectRouting(Track track)
    {
        if (track is not null)
        {
            StrongReferenceMessenger.Default.Unregister<SectionAdded>(this);
            StrongReferenceMessenger.Default.Unregister<SectionRemoved>(this);
            StrongReferenceMessenger.Default.Unregister<CalculationStarting>(this);
            StrongReferenceMessenger.Default.Unregister<CalculationFinished>(this);
            StrongReferenceMessenger.Default.Unregister<RouteChanging>(this);
            StrongReferenceMessenger.Default.Unregister<RouteChanged>(this);
            StrongReferenceMessenger.Default.Unregister<FileSplitChanged>(this);
        }
    }

    void IRecipient<SectionAdded>.Receive(SectionAdded _)
    {
        OnPropertyChanged(nameof(CanEditProfile));
    }

    void IRecipient<SectionRemoved>.Receive(SectionRemoved _)
    {
        OnPropertyChanged(nameof(CanEditProfile));
    }

    void IRecipient<CalculationStarting>.Receive(CalculationStarting _)
    {
        OnPropertyChanged(nameof(CompareSessionState));

        DownhillCost = Track.RouteBuilder.Profile.DownhillCost;
        DownhillCutoff = Track.RouteBuilder.Profile.DownhillCutoff;
        UphillCost = Track.RouteBuilder.Profile.UphillCost;
        UphillCutoff = Track.RouteBuilder.Profile.UphillCutoff;
        BikerPower = Track.RouteBuilder.Profile.BikerPower;
    }

    void IRecipient<CalculationFinished>.Receive(CalculationFinished _)
    {
        OnPropertyChanged(nameof(CompareSessionState));
    }

    void IRecipient<RouteChanging>.Receive(RouteChanging _)
    {
        OnPropertyChanged(nameof(TrackIsCalculating));
        OnPropertyChanged(nameof(CompareSessionState));

        CurrentSelection = Selection.Invalid;
    }

    void IRecipient<RouteChanged>.Receive(RouteChanged message)
    {
        OnPropertyChanged(nameof(TrackIsCalculating));
        OnPropertyChanged(nameof(CompareSessionState));

        if (!message.Initialization && !IsDraggingWayPoint)
        {
            SaveTrackAsync().FireAndForget();
        }
    }

    void IRecipient<FileSplitChanged>.Receive(FileSplitChanged message)
    {
        SaveTrackAsync().FireAndForget();
    }
}
