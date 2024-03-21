using System;
using System.Collections.Specialized;
using System.Linq;
using cycloid.Routing;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Maps;

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

    private DragState _dragStateTo;
    private DragState _dragStateFrom;

    private void Connect(Track track)
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

    private void Disconnect(Track track)
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

    public void BeginDrag((RouteSection To, RouteSection From) sections)
    {
        _dragStateTo = sections.To == null ? default : new DragState(GetSectionLine(sections.To), true);
        _dragStateFrom = sections.From == null ? default : new DragState(GetSectionLine(sections.From), false);
    }

    public void EndDrag()
    {
        _dragStateTo = _dragStateFrom = default;
    }

    private MapIcon GetWayPointIcon(WayPoint point) => _routingLayer.MapElements.OfType<MapIcon>().First(element => (WayPoint)element.Tag == point);

    private MapPolyline GetSectionLine(RouteSection section) => _routingLayer.MapElements.OfType<MapPolyline>().First(line => (RouteSection)line.Tag == section);

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
                    icon.Visible = false;
                    _routingLayer.MapElements.Remove(GetWayPointIcon(wayPoint));
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
    }

    private void RouteBuilder_FileSplitChanged(WayPoint wayPoint)
    {
        GetWayPointIcon(wayPoint).MapStyleSheetEntry = wayPoint.IsFileSplit ? "Routing.SplitPoint" : "Routing.Point";
    }
}
