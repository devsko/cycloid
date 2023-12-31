using CommunityToolkit.WinUI;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using Windows.Devices.Geolocation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Media;


namespace cycloid.Controls;

public sealed partial class Map : UserControl
{
    private readonly ClickPanel _clickPanel;
    private MapTileSource _heatmap;
    private MapTileSource _osm;
    private MapElementsLayer _routingLayer;

    public Track Track
    {
        get => (Track)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public static readonly DependencyProperty TrackProperty =
        DependencyProperty.Register(nameof(Track), typeof(Track), typeof(Map), new PropertyMetadata(null));

    public TrackPoint? CurrentPoint
    {
        get => (TrackPoint?)GetValue(CurrentPointProperty);
        set => SetValue(CurrentPointProperty, value);
    }

    public static readonly DependencyProperty CurrentPointProperty =
        DependencyProperty.Register(nameof(CurrentPoint), typeof(TrackPoint?), typeof(Map), new PropertyMetadata(null));

    public TrackPoint? HoverPoint
    {
        get => (TrackPoint?)GetValue(HoverPointProperty);
        set => SetValue(HoverPointProperty, value);
    }

    public static readonly DependencyProperty HoverPointProperty =
        DependencyProperty.Register(nameof(HoverPoint), typeof(TrackPoint?), typeof(Map), new PropertyMetadata(null));

    public bool HoverPointValuesEnabled
    {
        get => (bool)GetValue(HoverPointValuesPropertyEnabled);
        set => SetValue(HoverPointValuesPropertyEnabled, value);
    }

    public static DependencyProperty HoverPointValuesPropertyEnabled =
        DependencyProperty.Register(nameof(HoverPointValuesEnabled), typeof(bool), typeof(Map), new PropertyMetadata(false));

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

        //******************

        Routing.RouteBuilder builder = new();
        builder.CalculationFinished += (section, result) =>
        {
            if (result.IsValid)
            {
                _routingLayer.MapElements.Add(new MapPolyline
                {
                    Path = new Geopath(result.Points.Select(point => new BasicGeoposition { Latitude = point.Latitude, Longitude = point.Longitude })),
                    MapStyleSheetEntry = "Routing.Line",
                });
            }
        };
        builder.Points.CollectionChanged += (sender, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Add)
            {
                _routingLayer.MapElements.Add(new MapIcon
                {
                    Location = new Geopoint((MapPoint)args.NewItems[0]),
                    MapStyleSheetEntry = "Routing.Point",
                });
            }
        };
        builder.AddLastPoint(new MapPoint(48.187154f, 16.313179f));
        builder.AddLastPoint(new MapPoint(48.199672f, 16.254162f));
        builder.AddLastPoint(new MapPoint(48.201764f, 16.244299f));
        builder.AddLastPoint(new MapPoint(48.182811f, 16.122369f));
        builder.AddLastPoint(new MapPoint(48.108571f, 16.03775f));
        builder.AddLastPoint(new MapPoint(48.026322f, 15.921589f));
        builder.AddLastPoint(new MapPoint(47.961056f, 15.808882f));
        builder.AddLastPoint(new MapPoint(47.878235f, 15.629786f));
        builder.AddLastPoint(new MapPoint(47.871516f, 15.598135f));
        builder.AddLastPoint(new MapPoint(47.864178f, 15.592255f));
        builder.AddLastPoint(new MapPoint(47.862243f, 15.58887f));
        builder.AddLastPoint(new MapPoint(47.860939f, 15.58313f));
        builder.AddLastPoint(new MapPoint(47.857592f, 15.579903f));
        builder.AddLastPoint(new MapPoint(47.853269f, 15.572645f));
        builder.AddLastPoint(new MapPoint(47.852237f, 15.56537f));
        builder.AddLastPoint(new MapPoint(47.847267f, 15.553873f));
        builder.AddLastPoint(new MapPoint(47.841961f, 15.548225f));
        builder.AddLastPoint(new MapPoint(47.830391f, 15.549156f));
        builder.AddLastPoint(new MapPoint(47.816947f, 15.548799f));
        builder.AddLastPoint(new MapPoint(47.815398f, 15.544413f));
        builder.AddLastPoint(new MapPoint(47.817398f, 15.537541f));
        builder.AddLastPoint(new MapPoint(47.819608f, 15.531802f));
        builder.AddLastPoint(new MapPoint(47.820944f, 15.525935f));
        builder.AddLastPoint(new MapPoint(47.822569f, 15.520184f));
        builder.AddLastPoint(new MapPoint(47.829616f, 15.487246f));
        builder.AddLastPoint(new MapPoint(47.825069f, 15.472469f));
        builder.AddLastPoint(new MapPoint(47.808607f, 15.368826f));
        builder.AddLastPoint(new MapPoint(47.77284f, 15.317575f));


    }

    private class ClickPanel : Panel
    {
        public ClickPanel()
        {
            Background = new SolidColorBrush(Colors.Transparent);
        }

        protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
        {
            return new Windows.Foundation.Size(1e5, 1e5);
        }
    }

    private void RoutingLayer_MapElementPointerExited(MapElementsLayer sender, MapElementsLayerPointerExitedEventArgs args)
    {
        if (args.MapElement is MapIcon routePoint)
        {
            args.MapElement.MapStyleSheetEntryState = "";
        }
        else if (args.MapElement is MapPolyline routeLine)
        {
            args.MapElement.MapStyleSheetEntry = "Routing.Line";
        }
    }

    private void RoutingLayer_MapElementPointerEntered(MapElementsLayer sender, MapElementsLayerPointerEnteredEventArgs args)
    {
        if (args.MapElement is MapIcon routePoint)
        {
            args.MapElement.MapStyleSheetEntryState = "Routing.hover";
        }
        else if (args.MapElement is MapPolyline routeLine)
        { 
            args.MapElement.MapStyleSheetEntry = "Routing.HoveredLine";
        }
    }

    private void RoutingLayer_MapElementClick(MapElementsLayer sender, MapElementsLayerClickEventArgs args)
    {
        MapControl.Children.Add(_clickPanel);
    }

    private void ClickPanel_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        Debug.WriteLine("Panel_Tapped");
        MapControl.Children.Remove(_clickPanel);
    }

    private void ClickPanel_PointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Debug.WriteLine("Panel_PointerMoved");
    }

    private void MapControl_MapTapped(MapControl sender, MapInputEventArgs args)
    {
        Debug.WriteLine("MapControl_MapTapped");
    }

    private void MapControl_MapRightTapped(MapControl sender, MapRightTappedEventArgs args)
    {
        Debug.WriteLine("MapControl_MapRightTapped");
    }
}
