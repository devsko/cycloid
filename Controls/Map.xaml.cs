﻿using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.Web.Http.Filters;
using Windows.Web.Http;
using System;
using map;
using System.Linq;
using Windows.UI;
using System.Diagnostics;
using System.Collections.Specialized;
using Windows.Storage.Streams;
using Microsoft.Toolkit.Uwp.UI;
using Windows.Services.Maps;
using Windows.UI.Xaml.Media;


namespace cycloid.Controls;

public sealed partial class Map : UserControl
{
    private static readonly RandomAccessStreamReference _routePointImage = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/RoutePoint.png"));

    private MapTileSource _heatmap;
    private MapElementsLayer _trackLayer;
    private MapElementsLayer _routePointsLayer;

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

        // TrackLayer
        _trackLayer = (MapElementsLayer)MapControl.Resources["TrackLayer"];
        MapControl.Layers.Add(_trackLayer);

        // RoutePointsLayer
        _routePointsLayer = (MapElementsLayer)MapControl.Resources["RoutePointsLayer"];
        MapControl.Layers.Add(_routePointsLayer);

        _ = Async();

        async Task Async()
        {
            MapService.ServiceToken = "h2SdNrCncYPsgAfOlITE~4Yo41G6AvCkvigXhUwChig~AmjpNLnULNqoAMPx1H7BpgopNheySzWQC0aXrpBONC87WXgIfNSPaHBu32g1aQ8A";
            var result = await MapLocationFinder.FindLocationsAtAsync(new Geopoint(ViewModel.CurrentPoint.Value));

            (bool success, string uriFormat, HttpCookie[] cookies) = await new Strava().InitializeHeatmapAsync();
            if (success)
            {
                // Wird nicht verwendet!
                //heatmapSource.AdditionalRequestHeaders.Add("Cookie", cookies);

                // stattdessen:
                HttpBaseProtocolFilter filter = new();
                foreach (HttpCookie cookie in cookies)
                {
                    filter.CookieManager.SetCookie(cookie);
                }

                ((HttpMapTileDataSource)_heatmap.DataSource).UriFormatString = uriFormat;
                ViewModel.HeatmapVisible = true;
            }
        }


        //******************
        RouteBuilder builder = new();
        builder.CalculationFinished += (section, result) =>
        {
            if (result.IsValid)
            {
                Geopath path = new(result.Points.Select(point => new BasicGeoposition { Latitude = point.Latitude, Longitude = point.Longitude }));
                MapPolyline trackLine = new()
                {
                    Path = path,
                    StrokeColor = Colors.DeepPink,
                    StrokeThickness = 4,
                };
                _trackLayer.MapElements.Add(trackLine);
            }
        };
        builder.Points.CollectionChanged += (sender, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Add)
            {
                _routePointsLayer.MapElements.Add(new MapIcon
                {
                    Image = _routePointImage,
                    Location = new Geopoint((MapPoint)args.NewItems[0])
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

    private ClickPanel _clickPanel;

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

    private void RoutePointsLayer_MapElementPointerExited(MapElementsLayer sender, MapElementsLayerPointerExitedEventArgs args)
    {
        Debug.WriteLine("RoutePointsLayer_MapElementPointerExited");
    }

    private void RoutePointsLayer_MapElementPointerEntered(MapElementsLayer sender, MapElementsLayerPointerEnteredEventArgs args)
    {
        Debug.WriteLine("RoutePointsLayer_MapElementPointerEntered");
    }

    private void RoutePointsLayer_MapElementClick(MapElementsLayer sender, MapElementsLayerClickEventArgs args)
    {
        Debug.WriteLine("RoutePointsLayer_MapElementClick");
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

    private void TrackLayer_MapElementPointerExited(MapElementsLayer sender, MapElementsLayerPointerExitedEventArgs args)
    {
        Debug.WriteLine("TrackLayer_MapElementPointerExited");
    }

    private void TrackLayer_MapElementPointerEntered(MapElementsLayer sender, MapElementsLayerPointerEnteredEventArgs args)
    {
        Debug.WriteLine("TrackLayer_MapElementPointerEntered");
    }

    private void TrackLayer_MapElementClick(MapElementsLayer sender, MapElementsLayerClickEventArgs args)
    {
        Debug.WriteLine("TrackLayer_MapElementClick");
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