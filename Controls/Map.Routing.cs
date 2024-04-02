using System.Collections.Specialized;
using System;
using System.Diagnostics;
using System.Linq;
using cycloid.Routing;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Input;

namespace cycloid.Controls;

partial class Map
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
        static (point, @this) => @this.DragWayPoint(point),
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

    private void ConnectRouting(Track track)
    {
        track.Loaded += Track_Loaded;

        track.RouteBuilder.Points.CollectionChanged += RouteBuilderPoints_CollectionChanged;
        track.RouteBuilder.SectionAdded += RouteBuilder_SectionAdded;
        track.RouteBuilder.SectionRemoved += RouteBuilder_SectionRemoved;
        track.RouteBuilder.CalculationStarting += RouteBuilder_CalculationStarting;
        //track.RouteBuilder.CalculationDelayed += RouteBuilder_CalculationDelayed;
        track.RouteBuilder.CalculationRetry += RouteBuilder_CalculationRetry;
        track.RouteBuilder.CalculationFinished += RouteBuilder_CalculationFinished;
        track.RouteBuilder.FileSplitChanged += RouteBuilder_FileSplitChanged;
    }

    private void DisconnectRouting(Track track)
    {
        track.Loaded -= Track_Loaded;

        track.RouteBuilder.Points.CollectionChanged -= RouteBuilderPoints_CollectionChanged;
        track.RouteBuilder.SectionAdded -= RouteBuilder_SectionAdded;
        track.RouteBuilder.SectionRemoved -= RouteBuilder_SectionRemoved;
        track.RouteBuilder.CalculationStarting -= RouteBuilder_CalculationStarting;
        //track.RouteBuilder.CalculationDelayed -= RouteBuilder_CalculationDelayed;
        track.RouteBuilder.CalculationRetry -= RouteBuilder_CalculationRetry;
        track.RouteBuilder.CalculationFinished -= RouteBuilder_CalculationFinished;
        track.RouteBuilder.FileSplitChanged -= RouteBuilder_FileSplitChanged;
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

    private void HandleRoutingPanelPointerMoved(Point point)
    {
        _dragWayPointThrottle.Next(point, this);
    }

    private void DragWayPoint(Point point)
    {
        if (MapControl.TryGetLocationFromOffset(point, out Geopoint location))
        {
            ViewModel.ContinueDragWayPointAsync((MapPoint)location.Position).FireAndForget();
        }
    }

    private MapIcon GetWayPointIcon(WayPoint point) => _routingLayer.MapElements.OfType<MapIcon>().FirstOrDefault(element => (WayPoint)element.Tag == point);

    private MapPolyline GetSectionLine(RouteSection section) => _routingLayer.MapElements.OfType<MapPolyline>().FirstOrDefault(line => (RouteSection)line.Tag == section);

    private Visibility VisibleIfNotIsFileSplit(WayPoint wayPoint) => wayPoint?.IsFileSplit == false ? Visibility.Visible : Visibility.Collapsed;

    private Visibility VisibleIfNotIsDirectRoute(RouteSection section) => section?.IsDirectRoute == false ? Visibility.Visible : Visibility.Collapsed;

    private void RoutingLayer_MapElementPointerEntered(MapElementsLayer _, MapElementsLayerPointerEnteredEventArgs args)
    {
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
        args.MapElement.MapStyleSheetEntryState = "";

        ViewModel.HoveredWayPoint = null;
        ViewModel.HoveredSection = null;
    }

    private void RoutingLayer_MapElementClick(MapElementsLayer _, MapElementsLayerClickEventArgs args)
    {
        // No way to find out if it is a middle button click for delete. Use Ctrl-Click as workaround

        //bool ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);

        if (!ViewModel.IsCaptured)
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
                ViewModel.StartDragNewWayPointAsync((MapPoint)args.Location.Position).FireAndForget();
            }
        }
    }

    private void ViewModel_DragWayPointStarting(WayPoint wayPoint)
    {
        BeginDrag(ViewModel.Track.RouteBuilder.GetSections(wayPoint));
    }

    private void ViewModel_DragNewWayPointStarting(RouteSection section)
    {
        BeginDrag((section, section));
    }

    private void ViewModel_DragWayPointStarted()
    {
        PointerPanel.IsEnabled = true;

        GeneralTransform transform = Window.Current.Content.TransformToVisual(MapControl);
        Rect window = Window.Current.CoreWindow.Bounds;
        Point pointer = CoreWindow.GetForCurrentThread().PointerPosition;
        pointer = new Point(pointer.X - window.X, pointer.Y - window.Y);

        if (transform.TryTransform(pointer, out pointer) &&
            MapControl.TryGetLocationFromOffset(pointer, out Geopoint location))
        {
            ViewModel.ContinueDragWayPointAsync((MapPoint)location.Position).FireAndForget();
        }
    }

    private void ViewModel_DragWayPointEnded()
    {
        PointerPanel.IsEnabled = false;
        EndDrag();
    }

    private void Track_Loaded()
    {
        GeoboundingBox bounds = GeoboundingBox.TryCompute(ViewModel.Track.Points.Select(trackPoint => (BasicGeoposition)trackPoint));
        if (bounds is not null)
        {
            MapControl.TrySetViewBoundsAsync(bounds, new Thickness(25), MapAnimationKind.Bow).AsTask().FireAndForget();
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
                        Location = new Geopoint(newPoint.Location),
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
                    icon.Location = new Geopoint((BasicGeoposition)newPoint.Location);
                    icon.Tag = newPoint;
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                break;
        }
    }

    private void RouteBuilder_SectionAdded(RouteSection section, int index)
    {
        if (DragState.IsSameSection(_dragStateTo, _dragStateFrom, section))
        {
            MapPolyline line = new()
            {
                MapStyleSheetEntry = "Routing.Line",
                Tag = section,
                Path = new Geopath([new BasicGeoposition()])
            };
            _routingLayer.MapElements.Add(line);
            _dragStateFrom = new DragState(line, false);
            _dragStateFrom.Added(section);
        }
        else if (!_dragStateTo.Added(section) && !_dragStateFrom.Added(section))
        {
            _routingLayer.MapElements.Add(new MapPolyline
            {
                MapStyleSheetEntry = "Routing.Line",
                MapStyleSheetEntryState = "Routing.new",
                Tag = section,
                Path = new Geopath([(BasicGeoposition)section.Start.Location, (BasicGeoposition)section.End.Location]),
            });
        }
    }

    private void RouteBuilder_SectionRemoved(RouteSection section, int index)
    {
        if (!_dragStateTo.Removed(section) && !_dragStateFrom.Removed(section))
        {
            _routingLayer.MapElements.Remove(GetSectionLine(section));
        }

        if (section == ViewModel.HoveredSection)
        {
            ViewModel.HoveredSection = null;
        }
    }

    private void RouteBuilder_CalculationStarting(RouteSection section)
    {
        if (!ViewModel.IsCaptured)
        {
            GetSectionLine(section).MapStyleSheetEntryState = "Routing.calculating";
        }
    }

    //private void RouteBuilder_CalculationDelayed(RouteSection section)
    //{
    //    if (_dragStateTo.Delayed(section) || _dragStateFrom.Delayed(section))
    //    {

    //    }
    //}

    private void RouteBuilder_CalculationRetry(RouteSection section)
    {
        GetSectionLine(section).MapStyleSheetEntryState = "Routing.retry";
    }

    private void RouteBuilder_CalculationFinished(RouteSection section, RouteResult result)
    {
        if (!section.IsCanceled)
        {
            _dragStateTo.Calculated(section);
            _dragStateFrom.Calculated(section);

            MapPolyline line = GetSectionLine(section);
            if (result.IsValid)
            {
                line.Path = new Geopath(result.Points.Select(p => new BasicGeoposition { Longitude = p.Longitude, Latitude = p.Latitude }));
                line.MapStyleSheetEntryState = "";
            }
            else
            {
                if (line.Path.Positions.Count < 2)
                {
                    line.Path = new Geopath([(BasicGeoposition)section.Start.Location, (BasicGeoposition)section.End.Location]);
                }
                line.MapStyleSheetEntryState = "Routing.error";
            }
        }
        else
        {
            MapPolyline line = GetSectionLine(section);
            if (line is not null)
            {
                line.MapStyleSheetEntryState = "";
            }
        }
    }

    private void RouteBuilder_FileSplitChanged(WayPoint wayPoint)
    {
        GetWayPointIcon(wayPoint).MapStyleSheetEntry = wayPoint.IsFileSplit ? "Routing.SplitPoint" : "Routing.Point";
    }
}