using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using cycloid.Routing;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Media;

namespace cycloid.Controls;

partial class Map :
    IRecipient<DragWayPointStarting>,
    IRecipient<DragNewWayPointStarting>,
    IRecipient<DragWayPointStarted>,
    IRecipient<DragWayPointEnded>,
    IRecipient<SectionAdded>,
    IRecipient<SectionRemoved>,
    IRecipient<CalculationStarting>,
    IRecipient<CalculationRetry>,
    IRecipient<CalculationFinished>,
    IRecipient<FileSplitChanged>,
    IRecipient<SelectionChanged>
{
    private struct DragState(MapPolyline line, bool compareStart)
    {
        private RouteSection _oldSection = (RouteSection)line.Tag;
        private RouteSection _newSection;

        public static bool IsSameSection(DragState dragStateTo, DragState dragStateFrom, RouteSection section)
            => dragStateTo._oldSection == dragStateFrom._oldSection && section.End == dragStateFrom._oldSection?.End;

        public bool Added(RouteSection section)
        {
            if (compareStart ? section.Start == _oldSection?.Start : section.End == _oldSection?.End)
            {
                line.Tag = _newSection = section;
                return true;
            }
            return false;
        }

        public bool Removed(RouteSection section)
        {
            if (section == _oldSection)
            {
                return true;
            }
            if (section == _newSection)
            {
                _newSection = null;
                return true;
            }
            return false;
        }

        //public bool Delayed(RouteSection section)
        //{
        //    if (section == _newSection)
        //    {
        //        line.Tag = _oldSection;
        //        this = default;
        //        return true;
        //    }
        //    return false;
        //}

        public bool Calculated(RouteSection section)
        {
            if (section == _newSection)
            {
                _oldSection = section;
                _newSection = null;
                return true;
            }
            return false;
        }
    }

    private readonly Throttle<Point, Map> _dragWayPointThrottle = new(
        static (value, @this) => @this.DragWayPoint(value), 
        TimeSpan.FromMilliseconds(100));

    private DragState _dragStateTo;
    private DragState _dragStateFrom;

    public void BeginDrag((RouteSection To, RouteSection From) sections)
    {
        _dragStateTo = sections.To == null ? default : new DragState(GetSectionLine(sections.To), true);
        _dragStateFrom = sections.From == null ? default : new DragState(GetSectionLine(sections.From), false);
    }

    public void EndDrag()
    {
        _dragStateTo = _dragStateFrom = default;
    }

    private void RegisterRoutingMessages()
    {
        StrongReferenceMessenger.Default.Register<DragWayPointStarting>(this);
        StrongReferenceMessenger.Default.Register<DragNewWayPointStarting>(this);
        StrongReferenceMessenger.Default.Register<DragWayPointStarted>(this);
        StrongReferenceMessenger.Default.Register<DragWayPointEnded>(this);
        StrongReferenceMessenger.Default.Register<SelectionChanged>(this);
   }

    private void ConnectRouting(Track track)
    {
        if (track is not null)
        {
            track.RouteBuilder.Points.CollectionChanged += RouteBuilderPoints_CollectionChanged;

            StrongReferenceMessenger.Default.Register<SectionAdded>(this);
            StrongReferenceMessenger.Default.Register<SectionRemoved>(this);
            StrongReferenceMessenger.Default.Register<CalculationStarting>(this);
            StrongReferenceMessenger.Default.Register<CalculationRetry>(this);
            StrongReferenceMessenger.Default.Register<CalculationFinished>(this);
            StrongReferenceMessenger.Default.Register<FileSplitChanged>(this);

            //track.RouteBuilder.CalculationDelayed += RouteBuilder_CalculationDelayed;
        }
    }

    private void DisconnectRouting(Track track)
    {
        if (track is not null)
        {
            track.RouteBuilder.Points.CollectionChanged -= RouteBuilderPoints_CollectionChanged;

            StrongReferenceMessenger.Default.Unregister<SectionAdded>(this);
            StrongReferenceMessenger.Default.Unregister<SectionRemoved>(this);
            StrongReferenceMessenger.Default.Unregister<CalculationStarting>(this);
            StrongReferenceMessenger.Default.Unregister<CalculationRetry>(this);
            StrongReferenceMessenger.Default.Unregister<CalculationFinished>(this);
            StrongReferenceMessenger.Default.Unregister<FileSplitChanged>(this);

            //track.RouteBuilder.CalculationDelayed -= RouteBuilder_CalculationDelayed;
        }
    }

    private bool HandleRoutingKey(VirtualKey key)
    {
        if (key == VirtualKey.Escape)
        {
            ViewModel.CancelDragWayPointAsync().FireAndForget();
            return true;
        }

        return false;
    }

    private void HandleRoutingPanelTapped()
    {
        ViewModel.EndDragWayPoint();
    }

    private void HandleRoutingPointerMoved(Point point)
    {
        _dragWayPointThrottle.Next(point, this);
    }

    private void DragWayPoint(Point point)
    {
        if (MapControl.TryGetLocationFromOffset(point, out Geopoint location))
        {
            ViewModel.ContinueDragWayPointAsync(location.Position.ToMapPoint()).FireAndForget();
        }
    }

    private MapIcon GetWayPointIcon(WayPoint point) => _routingLayer.MapElements.OfType<MapIcon>().FirstOrDefault(element => (WayPoint)element.Tag == point);

    private MapPolyline GetSectionLine(RouteSection section) => _routingLayer.MapElements.OfType<MapPolyline>().FirstOrDefault(line => (RouteSection)line.Tag == section);

    private Visibility VisibleIfNotIsFileSplit(WayPoint wayPoint) => wayPoint?.IsFileSplit == false ? Visibility.Visible : Visibility.Collapsed;

    private Visibility VisibleIfNotIsDirectRoute(RouteSection section) => section?.IsDirectRoute == false ? Visibility.Visible : Visibility.Collapsed;

    private void RoutingLayer_MapElementPointerEntered(MapElementsLayer _, MapElementsLayerPointerEnteredEventArgs args)
    {
        if (ViewModel.Mode != Modes.Edit)
        {
            return;
        }

        Debug.Assert(ViewModel.HoveredWayPoint is null && ViewModel.HoveredSection is null);

        if (args.MapElement is MapIcon { Tag: WayPoint wayPoint })
        {
            args.MapElement.MapStyleSheetEntryState = "Routing.hover";
            ViewModel.HoveredWayPoint = wayPoint;
        }
        else if (args.MapElement is MapPolyline { Tag: RouteSection section })
        {
            args.MapElement.MapStyleSheetEntryState = "Routing.hovered";
            ViewModel.HoveredSection = section;
        }
    }

    private void RoutingLayer_MapElementPointerExited(MapElementsLayer _, MapElementsLayerPointerExitedEventArgs args)
    {
        if (ViewModel.Mode != Modes.Edit)
        {
            return;
        }

        args.MapElement.MapStyleSheetEntryState = "";

        ViewModel.HoveredWayPoint = null;
        ViewModel.HoveredSection = null;
    }

    private void RoutingLayer_MapElementClick(MapElementsLayer _, MapElementsLayerClickEventArgs args)
    {
        if (ViewModel.Mode != Modes.Edit)
        {
            return;
        }

        // No way to find out if it is a middle button click for delete. Use Ctrl-Click as workaround

        //bool ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);

        if (!ViewModel.IsDraggingWayPoint)
        {
            if (ViewModel.HoveredWayPoint is not null)
            {
                // PROBLEM: MapControl_MapTapped is raised additionaly. After deleting the waypoint there is no way to detect it was just element clicked
                //if (ctrl)
                //{
                //    ViewModel.DeleteWayPointAsync().FireAndForget();
                //}
                //else
                {
                    ViewModel.StartDragWayPoint();
                }
            }
            else if (ViewModel.HoveredSection is not null)
            {
                ViewModel.StartDragNewWayPointAsync(args.Location.Position.ToMapPoint()).FireAndForget();
            }
        }
    }

    private void RouteBuilderPoints_CollectionChanged(object _, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                {
                    WayPoint newPoint = (WayPoint)e.NewItems[0];
                    _routingLayer.MapElements.Add(new MapIcon
                    {
                        Location = new Geopoint(newPoint.Location.ToBasicGeoposition()),
                        Tag = newPoint,
                        NormalizedAnchorPoint = new Point(.5, .5),
                        MapStyleSheetEntry = newPoint.IsFileSplit ? "Routing.SplitPoint" : "Routing.Point",
                    });
                }
                break;
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Remove:
                {
                    WayPoint wayPoint = (WayPoint)e.OldItems[0];
                    if (ViewModel.HoveredWayPoint == wayPoint)
                    {
                        ViewModel.HoveredWayPoint = null;
                    }
                    MapIcon icon = GetWayPointIcon(wayPoint);
                    _routingLayer.MapElements.Remove(icon);
                }
                break;
            case NotifyCollectionChangedAction.Replace:
                {
                    WayPoint oldPoint = (WayPoint)e.OldItems[0];
                    WayPoint newPoint = (WayPoint)e.NewItems[0];
                    if (ViewModel.HoveredWayPoint == oldPoint)
                    {
                        ViewModel.HoveredWayPoint = newPoint;
                    }
                    MapIcon icon = GetWayPointIcon(oldPoint);
                    icon.Location = new Geopoint(newPoint.Location.ToBasicGeoposition());
                    icon.Tag = newPoint;
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                break;
        }
    }

    void IRecipient<DragWayPointStarting>.Receive(DragWayPointStarting message)
    {
        BeginDrag(ViewModel.Track.RouteBuilder.GetSections(message.WayPoint));
    }

    void IRecipient<DragNewWayPointStarting>.Receive(DragNewWayPointStarting message)
    {
        BeginDrag((message.Section, message.Section));
    }

    void IRecipient<DragWayPointStarted>.Receive(DragWayPointStarted message)
    {
        PointerPanel.IsEnabled = true;

        GeneralTransform transform = Window.Current.Content.TransformToVisual(MapControl);
        Rect window = Window.Current.CoreWindow.Bounds;
        Point pointer = CoreWindow.GetForCurrentThread().PointerPosition;
        pointer = new Point(pointer.X - window.X, pointer.Y - window.Y);

        if (transform.TryTransform(pointer, out pointer) &&
            MapControl.TryGetLocationFromOffset(pointer, out Geopoint location))
        {
            ViewModel.ContinueDragWayPointAsync(location.Position.ToMapPoint()).FireAndForget();
        }
    }

    void IRecipient<DragWayPointEnded>.Receive(DragWayPointEnded _)
    {
        PointerPanel.IsEnabled = false;
        EndDrag();
    }

    void IRecipient<SectionAdded>.Receive(SectionAdded message)
    {
        if (DragState.IsSameSection(_dragStateTo, _dragStateFrom, message.Section))
        {
            MapPolyline line = new()
            {
                MapStyleSheetEntry = "Routing.Line",
                Tag = message.Section,
                Path = new Geopath([new BasicGeoposition()])
            };
            _routingLayer.MapElements.Add(line);
            _dragStateFrom = new DragState(line, false);
            _dragStateFrom.Added(message.Section);
        }
        else if (!_dragStateTo.Added(message.Section) && !_dragStateFrom.Added(message.Section))
        {
            _routingLayer.MapElements.Add(new MapPolyline
            {
                MapStyleSheetEntry = "Routing.Line",
                MapStyleSheetEntryState = "Routing.new",
                Tag = message.Section,
                Path = new Geopath([message.Section.Start.Location.ToBasicGeoposition(), message.Section.End.Location.ToBasicGeoposition()]),
            });
        }
    }

    void IRecipient<SectionRemoved>.Receive(SectionRemoved message)
    {
        if (!_dragStateTo.Removed(message.Section) && !_dragStateFrom.Removed(message.Section))
        {
            _routingLayer.MapElements.Remove(GetSectionLine(message.Section));
        }

        if (message.Section == ViewModel.HoveredSection)
        {
            ViewModel.HoveredSection = null;
        }
    }

    void IRecipient<CalculationStarting>.Receive(CalculationStarting message)
    {
        if (!ViewModel.IsDraggingWayPoint)
        {
            GetSectionLine(message.Section).MapStyleSheetEntryState = "Routing.calculating";
        }
    }

    void IRecipient<CalculationRetry>.Receive(CalculationRetry message)
    {
        GetSectionLine(message.Section).MapStyleSheetEntryState = "Routing.retry";
    }

    void IRecipient<CalculationFinished>.Receive(CalculationFinished message)
    {
        if (!message.Section.IsCanceled)
        {
            _dragStateTo.Calculated(message.Section);
            _dragStateFrom.Calculated(message.Section);

            MapPolyline line = GetSectionLine(message.Section);
            if (message.Result.IsValid)
            {
                line.Path = new Geopath(message.Result.Points.Select(p => new BasicGeoposition { Longitude = p.Longitude, Latitude = p.Latitude }));
                line.MapStyleSheetEntryState = "";
            }
            else
            {
                if (line.Path.Positions.Count < 2)
                {
                    line.Path = new Geopath([message.Section.Start.Location.ToBasicGeoposition(), message.Section.End.Location.ToBasicGeoposition()]);
                }
                line.MapStyleSheetEntryState = "Routing.error";
            }
        }
        else
        {
            MapPolyline line = GetSectionLine(message.Section);
            if (line is not null)
            {
                line.MapStyleSheetEntryState = "";
            }
        }
    }

    void IRecipient<FileSplitChanged>.Receive(FileSplitChanged message)
    {
        GetWayPointIcon(message.WayPoint).MapStyleSheetEntry = message.WayPoint.IsFileSplit ? "Routing.SplitPoint" : "Routing.Point";
    }

    void IRecipient<SelectionChanged>.Receive(SelectionChanged message)
    {
        _routingLayer.MapElements.Remove(_selectionLine);
        if (message.Value.IsValid)
        {
            BasicGeoposition[] positions = ViewModel.Track.Points
                .Enumerate(message.Value.Start.Distance, message.Value.End.Distance)
                .Select(p => p.Location.ToBasicGeoposition())
                .ToArray();
            if (positions.Length > 0)
            {
                _selectionLine = new MapPolyline
                {
                    Path = new Geopath(positions),
                    MapStyleSheetEntry = "Routing.Selection",
                    ZIndex = -100,
                };
                _routingLayer.MapElements.Add(_selectionLine);
            }
        }
    }

    //private void RouteBuilder_CalculationDelayed(RouteSection section)
    //{
    //    if (_dragStateTo.Delayed(section) || _dragStateFrom.Delayed(section))
    //    {

    //    }
    //}
}