using System.Collections.Specialized;
using System.Linq;
using cycloid.Routing;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI.Xaml.Controls.Maps;

namespace cycloid.Controls;

partial class Map
{
    private class RouteBuilderAdapter
    {
        private struct DragState
        {
            private readonly MapPolyline _line;
            private readonly bool _compareStart;
            private RouteSection _oldSection;
            private RouteSection _newSection;

            public DragState(MapPolyline line, bool compareStart)
            {
                _line = line;
                _oldSection = (RouteSection)line.Tag;
                _compareStart = compareStart;
            }

            public static bool IsSameSection(DragState dragStateTo, DragState dragStateFrom, RouteSection section) 
                => dragStateTo._oldSection == dragStateFrom._oldSection && section.End == dragStateFrom._oldSection?.End;

            public bool Added(RouteSection section)
            {
                if (_compareStart ? section.Start == _oldSection?.Start : section.End == _oldSection?.End)
                {
                    _line.Tag = section;
                    _newSection = section;
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

        private readonly RouteBuilder _routeBuilder;
        private readonly MapElementsLayer _routingLayer;
        private readonly ViewModel _viewModel;
        private DragState _dragStateTo;
        private DragState _dragStateFrom;

        public RouteBuilderAdapter(RouteBuilder routeBuilder, MapElementsLayer routingLayer, ViewModel viewModel)
        {
            _routeBuilder = routeBuilder;
            _routingLayer = routingLayer;
            _viewModel = viewModel;

            routeBuilder.Points.CollectionChanged += RouteBuilderPoints_CollectionChanged;
            routeBuilder.SectionAdded += RouteBuilder_SectionAdded;
            routeBuilder.SectionRemoved += RouteBuilder_SectionRemoved;
            routeBuilder.CalculationStarting += RouteBuilder_CalculationStarting;
            routeBuilder.CalculationRetry += RouteBuilder_CalculationRetry;
            routeBuilder.CalculationFinished += RouteBuilder_CalculationFinished;

            viewModel.FileSplitChanged += ViewModel_FileSplitChanged;
        }

        public void Disconnect()
        {
            _routeBuilder.Points.CollectionChanged -= RouteBuilderPoints_CollectionChanged;
            _routeBuilder.SectionAdded -= RouteBuilder_SectionAdded;
            _routeBuilder.SectionRemoved -= RouteBuilder_SectionRemoved;
            _routeBuilder.CalculationStarting -= RouteBuilder_CalculationStarting;
            _routeBuilder.CalculationRetry -= RouteBuilder_CalculationRetry;
            _routeBuilder.CalculationFinished -= RouteBuilder_CalculationFinished;

            _viewModel.FileSplitChanged -= ViewModel_FileSplitChanged;
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
                        if (_viewModel.HoveredWayPoint == wayPoint)
                        {
                            _viewModel.HoveredWayPoint = null;
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
                        if (_viewModel.HoveredWayPoint == oldPoint)
                        {
                            _viewModel.HoveredWayPoint = newPoint;
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
                MapPolyline line = new() { Tag = section, Path = new Geopath([new BasicGeoposition()]) };
                _routingLayer.MapElements.Add(line);
                _dragStateFrom = new DragState(line, false);
                _dragStateFrom.Added(section);
            }
            else if (!_dragStateTo.Added(section) && 
                !_dragStateFrom.Added(section))
            {
                _routingLayer.MapElements.Add(new MapPolyline()
                {
                    MapStyleSheetEntry = "Routing.NewLine",
                    Tag = section,
                    Path = new Geopath([(BasicGeoposition)section.Start.Location, (BasicGeoposition)section.End.Location]),
                });
            }
        }

        private void RouteBuilder_SectionRemoved(RouteSection section, int index)
        {
            if (!_dragStateTo.Removed(section) && 
                !_dragStateFrom.Removed(section))
            {
                _routingLayer.MapElements.Remove(GetSectionLine(section));
            }

            if (section == _viewModel.HoveredSection)
            {
                _viewModel.HoveredSection = null;
            }
        }

        private void RouteBuilder_CalculationStarting(RouteSection section)
        {
            GetSectionLine(section).MapStyleSheetEntry = "Routing.CalculatingLine";
        }

        private void RouteBuilder_CalculationRetry(RouteSection section)
        {
            GetSectionLine(section).MapStyleSheetEntry = "Routing.RetryLine";
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
                    line.MapStyleSheetEntry = "Routing.Line";
                }
                else
                {
                    line.MapStyleSheetEntry = "Routing.ErrorLine";
                }
            }
        }

        private void ViewModel_FileSplitChanged(WayPoint wayPoint)
        {
            GetWayPointIcon(wayPoint).MapStyleSheetEntry = wayPoint.IsFileSplit ? "Routing.SplitPoint" : "Routing.Point";
        }
    }
}