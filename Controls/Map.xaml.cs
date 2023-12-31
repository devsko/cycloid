using CommunityToolkit.WinUI;
using cycloid.Routing;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;


namespace cycloid.Controls;

public sealed partial class Map : UserControl
{
    private class ClickPanel : Panel
    {
        public ClickPanel()
        {
            Background = new SolidColorBrush(Colors.Transparent);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(1e5, 1e5);
        }
    }

    private readonly ClickPanel _clickPanel;
    private MapTileSource _heatmap;
    private MapTileSource _osm;
    private MapElementsLayer _routingLayer;

    public Map()
    {
        InitializeComponent();

        _clickPanel = new ClickPanel();
        _clickPanel.PointerMoved += ClickPanel_PointerMoved;
        _clickPanel.Tapped += ClickPanel_Tapped;
    }

    private ViewModel ViewModel => (ViewModel)this.FindResource(nameof(ViewModel));

    private void MapControl_Loaded(object sender, RoutedEventArgs e)
    {
        MapControl.Center = new Geopoint(new BasicGeoposition() { Latitude = 46.46039124618558, Longitude = 10.089039490153148 });
        MapControl.ZoomLevel = 7;

        // Heatmap
        _heatmap = (MapTileSource)MapControl.Resources["Heatmap"];
        MapControl.Resources.Remove("Heatmap");
        MapControl.TileSources.Add(_heatmap);

        // Osm
        _osm = (MapTileSource)MapControl.Resources["Osm"];
        MapControl.Resources.Remove("Osm");
        MapControl.TileSources.Add(_osm);

        // RoutingLayer
        _routingLayer = (MapElementsLayer)MapControl.Resources["RoutingLayer"];
        MapControl.Layers.Add(_routingLayer);

        ViewModel.TrackChanged += ViewModel_TrackChanged;
    }

    private void RoutingLayer_MapElementPointerExited(MapElementsLayer sender, MapElementsLayerPointerExitedEventArgs args)
    {
        if (args.MapElement is MapIcon)
        {
            args.MapElement.MapStyleSheetEntryState = "";
        }
        else if (args.MapElement is MapPolyline)
        {
            args.MapElement.MapStyleSheetEntry = "Routing.Line";
        }
    }

    private void RoutingLayer_MapElementPointerEntered(MapElementsLayer sender, MapElementsLayerPointerEnteredEventArgs args)
    {
        if (args.MapElement is MapIcon)
        {
            args.MapElement.MapStyleSheetEntryState = "Routing.hover";
        }
        else if (args.MapElement is MapPolyline)
        { 
            args.MapElement.MapStyleSheetEntry = "Routing.HoveredLine";
        }
    }

    private void RoutingLayer_MapElementClick(MapElementsLayer sender, MapElementsLayerClickEventArgs args)
    {
        if (args.MapElements.OfType<MapIcon>().FirstOrDefault() is MapIcon routePoint)
        {
            ViewModel.StartDrag((MapPoint)routePoint.Tag);
        }
        else if (args.MapElements.OfType<MapPolyline>().FirstOrDefault() is MapPolyline sectionLine)
        {
            ViewModel.StartDrag((RouteSection)sectionLine.Tag, (MapPoint)args.Location.Position);
        }

        if (ViewModel.IsCaptured)
        {
            MapControl.Children.Add(_clickPanel);
        }            
    }

    private void ClickPanel_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.EndDrag();
        MapControl.Children.Remove(_clickPanel);
    }

    private void ClickPanel_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (MapControl.TryGetLocationFromOffset(e.GetCurrentPoint(MapControl).Position, out Geopoint location))
        {
            ViewModel.ContinueDrag((MapPoint)location.Position);
        }
    }

    private void MapControl_MapTapped(MapControl sender, MapInputEventArgs args)
    {
        Debug.WriteLine("MapControl_MapTapped");
    }

    private void MapControl_MapRightTapped(MapControl sender, MapRightTappedEventArgs args)
    {
        Debug.WriteLine("MapControl_MapRightTapped");
    }

    private void ViewModel_TrackChanged(Track oldTrack, Track newTrack)
    {
        _routingLayer.MapElements.Clear();

        if (oldTrack is not null)
        {
            oldTrack.RouteBuilder.Points.CollectionChanged -= RouteBuilderPoints_CollectionChanged;
            oldTrack.RouteBuilder.SectionAdded -= RouteBuilder_SectionAdded;
            oldTrack.RouteBuilder.SectionRemoved -= RouteBuilder_SectionRemoved;
            oldTrack.RouteBuilder.CalculationStarting -= RouteBuilder_CalculationStarting;
            oldTrack.RouteBuilder.CalculationRetry -= RouteBuilder_CalculationRetry;
            oldTrack.RouteBuilder.CalculationFinished -= RouteBuilder_CalculationFinished;
        }
        if (newTrack is not null)
        {
            newTrack.RouteBuilder.Points.CollectionChanged += RouteBuilderPoints_CollectionChanged;
            newTrack.RouteBuilder.SectionAdded += RouteBuilder_SectionAdded;
            newTrack.RouteBuilder.SectionRemoved += RouteBuilder_SectionRemoved;
            newTrack.RouteBuilder.CalculationStarting += RouteBuilder_CalculationStarting;
            newTrack.RouteBuilder.CalculationRetry += RouteBuilder_CalculationRetry;
            newTrack.RouteBuilder.CalculationFinished += RouteBuilder_CalculationFinished;
        }

        void RouteBuilderPoints_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    {
                        MapPoint newPoint = (MapPoint)e.NewItems[0];
                        _routingLayer.MapElements.Add(new MapIcon
                        {
                            Location = new Geopoint(newPoint),
                            Tag = newPoint,
                            NormalizedAnchorPoint = new Point(.5, .5),
                            MapStyleSheetEntry = "Routing.Point",
                        });
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                case NotifyCollectionChangedAction.Remove:
                    _routingLayer.MapElements.Remove(GetRoutePoint((MapPoint)e.OldItems[0]));
                    break;
                case NotifyCollectionChangedAction.Replace:
                    {
                        MapPoint oldPoint = (MapPoint)e.OldItems[0];
                        MapPoint newPoint = (MapPoint)e.NewItems[0];
                        MapIcon icon = GetRoutePoint(oldPoint);
                        icon.Location = new Geopoint(new BasicGeoposition { Latitude = newPoint.Latitude, Longitude = newPoint.Longitude });
                        icon.Tag = newPoint;
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    break;
            }

            MapIcon GetRoutePoint(MapPoint point) => _routingLayer.MapElements.OfType<MapIcon>().First(element => (MapPoint)element.Tag == point);
        }

        void RouteBuilder_SectionAdded(RouteSection section, int index)
        {
            MapPolyline sectionLine = new()
            {
                MapStyleSheetEntry = "Routing.NewLine",
                Tag = section,
                Path = new Geopath(new[] { new BasicGeoposition { Longitude = section.Start.Longitude, Latitude = section.Start.Latitude }, new BasicGeoposition { Longitude = section.End.Longitude, Latitude = section.End.Latitude } })
            };
            _routingLayer.MapElements.Add(sectionLine);
        }

        void RouteBuilder_SectionRemoved(RouteSection section, int index)
        {
            _routingLayer.MapElements.Remove(GetSectionLine(section));
        }

        void RouteBuilder_CalculationStarting(RouteSection section)
        {
            GetSectionLine(section).MapStyleSheetEntry = "Routing.CalculatingLine";
        }

        void RouteBuilder_CalculationRetry(RouteSection section)
        {
            GetSectionLine(section).MapStyleSheetEntry = "Routing.RetryLine";
        }

        void RouteBuilder_CalculationFinished(RouteSection section, RouteResult result)
        {
            if (!section.IsCanceled)
            {
                MapPolyline sectionLine = GetSectionLine(section);
                if (result.IsValid)
                {
                    sectionLine.Path = new Geopath(result.Points.Select(p => new BasicGeoposition { Longitude = p.Longitude, Latitude = p.Latitude }));
                    sectionLine.MapStyleSheetEntry = "Routing.Line";
                }
                else
                {
                    sectionLine.MapStyleSheetEntry = "Routing.ErrorLine";
                }
            }
        }

        MapPolyline GetSectionLine(RouteSection section) => _routingLayer.MapElements.OfType<MapPolyline>().First(line => line.Tag == section);
    }
}
