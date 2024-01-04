using cycloid.Routing;
using System.Collections.Specialized;
using System.Linq;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI.Xaml.Controls.Maps;

namespace cycloid.Controls;

partial class Map
{
    private class RouteBuilderAdapter
    {
        private readonly RouteBuilder _routeBuilder;
        private readonly MapElementsLayer _routingLayer;
        private readonly ViewModel _viewModel;
        private MapPolyline _cachedLine;

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
        }

        public void Disconnect()
        {
            _routeBuilder.Points.CollectionChanged -= RouteBuilderPoints_CollectionChanged;
            _routeBuilder.SectionAdded -= RouteBuilder_SectionAdded;
            _routeBuilder.SectionRemoved -= RouteBuilder_SectionRemoved;
            _routeBuilder.CalculationStarting -= RouteBuilder_CalculationStarting;
            _routeBuilder.CalculationRetry -= RouteBuilder_CalculationRetry;
            _routeBuilder.CalculationFinished -= RouteBuilder_CalculationFinished;
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
                            MapStyleSheetEntry = "Routing.Point",
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
            Geopath path = new([(BasicGeoposition)section.Start.Location, (BasicGeoposition)section.End.Location]);

            if (_cachedLine is MapPolyline line)
            {
                _cachedLine = null;
                line.MapStyleSheetEntry = "Routing.NewLine";
                line.Tag = section;
                line.Path = path;
                line.Visible = true;
            }
            else
            {
                _routingLayer.MapElements.Add(new MapPolyline()
                {
                    MapStyleSheetEntry = "Routing.NewLine",
                    Tag = section,
                    Path = path,
                });
            }
        }

        private void RouteBuilder_SectionRemoved(RouteSection section, int index)
        {
            MapPolyline line = GetSectionLine(section);
            if (section == _viewModel.HoveredSection)
            {
                _viewModel.HoveredSection = null;
            }
            line.Visible = false;
            if (_cachedLine is null)
            {
                _cachedLine = line;
            }
            else
            {
                _routingLayer.MapElements.Remove(line);
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
    }
}