using System;
using CommunityToolkit.WinUI;
using cycloid.Routing;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
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

    private readonly Throttle<Point, Map> _pointerMovedThrottle = new(
        static (point, map) => map.ThrottledClickPanelPointerMoved(point),
        TimeSpan.FromMilliseconds(100));

    private readonly ClickPanel _clickPanel;
    private MapTileSource _heatmap;
    private MapTileSource _osm;
    private MapElementsLayer _routingLayer;
    private RouteBuilderAdapter _routeBuilderAdapter;

    public Map()
    {
        InitializeComponent();

        _clickPanel = new ClickPanel();
        _clickPanel.PointerMoved += ClickPanel_PointerMoved;
        _clickPanel.Tapped += ClickPanel_Tapped;
    }

    private ViewModel ViewModel => (ViewModel)this.FindResource(nameof(ViewModel));

    private bool IsFileSplit(WayPoint wayPoint)
    {
        return false;
    }

    private Visibility VisibleIfNotIsFileSplit(WayPoint wayPoint) => !IsFileSplit(wayPoint) ? Visibility.Visible : Visibility.Collapsed;

    private bool IsDirectRoute(RouteSection section)
    {
        return section is { IsDirectRoute: true };
    }

    private Visibility VisibleIfNotIsDirectRoute(RouteSection section) => !IsDirectRoute(section) ? Visibility.Visible : Visibility.Collapsed;

    private void MapControl_Loaded(object _1, RoutedEventArgs _2)
    {
        ViewModel.TrackChanged += ViewModel_TrackChanged;
        ViewModel.DragWayPointStarted += ViewModel_DragWayPointStarted;
        ViewModel.DragWayPointEnded += ViewModel_DragWayPointEnded;

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
    }

    private void RoutingLayer_MapElementPointerEntered(MapElementsLayer _, MapElementsLayerPointerEnteredEventArgs args)
    {
        System.Diagnostics.Debug.Assert(ViewModel.HoveredWayPoint is null && ViewModel.HoveredSection is null);

        if (args.MapElement is MapIcon { Tag: WayPoint wayPoint })
        {
            args.MapElement.MapStyleSheetEntryState = "Routing.hover";
            ViewModel.HoveredWayPoint = wayPoint;
        }
        else if (args.MapElement is MapPolyline { Tag: RouteSection section })
        {
            args.MapElement.MapStyleSheetEntry = "Routing.HoveredLine";
            ViewModel.HoveredSection = section;
        }
    }

    private void RoutingLayer_MapElementPointerExited(MapElementsLayer _, MapElementsLayerPointerExitedEventArgs args)
    {
        if (args.MapElement is MapIcon { Tag: WayPoint wayPoint })
        {
            System.Diagnostics.Debug.Assert(wayPoint == ViewModel.HoveredWayPoint || (ViewModel.HoveredWayPoint is null && !args.MapElement.Visible));

            args.MapElement.MapStyleSheetEntryState = "";
        }
        else if (args.MapElement is MapPolyline { Tag: RouteSection section })
        {
            System.Diagnostics.Debug.Assert(section == ViewModel.HoveredSection || ViewModel.HoveredSection is null);

            args.MapElement.MapStyleSheetEntry = "Routing.Line";
        }

        ViewModel.HoveredWayPoint = null;
        ViewModel.HoveredSection = null;
    }

    private void RoutingLayer_MapElementClick(MapElementsLayer _, MapElementsLayerClickEventArgs args)
    {
        // No way to find out if it is a middle button click for delete. Use Ctrl-Click as workaround

        bool ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);

        if (!ViewModel.IsCaptured)
        {
            if (ViewModel.HoveredWayPoint is not null)
            {
                if (ctrl)
                {
                    ViewModel.DeleteWayPointAsync().FireAndForget();
                }
                else
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

    private void ClickPanel_Tapped(object _, TappedRoutedEventArgs e)
    {
        ViewModel.EndDragWayPoint();
        e.Handled = true;
    }

    private void ClickPanel_PointerMoved(object _, PointerRoutedEventArgs e)
    {
        _pointerMovedThrottle.Next(e.GetCurrentPoint(MapControl).Position, this);
        e.Handled = true;
    }

    private void ThrottledClickPanelPointerMoved(Point point)
    {
        if (MapControl.TryGetLocationFromOffset(point, out Geopoint location))
        {
            ViewModel.ContinueDragWayPointAsync((MapPoint)location.Position).FireAndForget();
        }
    }

    private void MapControl_MapTapped(MapControl _, MapInputEventArgs args)
    {
        // This is raised ADDITIONALY to MapElementClick!
        if (ViewModel.HoveredWayPoint is not null || ViewModel.HoveredSection is not null)
        {
            return;
        }

        bool ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);

        if (ctrl)
        {
            ViewModel.AddDestinationAsync((MapPoint)args.Location.Position).FireAndForget();
        }
    }

    private void MapControl_MapContextRequested(MapControl sender, MapContextRequestedEventArgs args)
    {
        MapCommandBarFlyout menu;
        MapPoint location = (MapPoint)args.Location.Position;
        if (ViewModel.Track is null)
        {
            menu = MapNoTrackMenu;
        }
        else if (ViewModel.HoveredWayPoint is not null)
        {
            menu = MapEditPointMenu;
        }
        else if (ViewModel.HoveredSection is not null)
        {
            menu = MapEditSectionMenu;
        }
        else
        {
            menu = MapEditMenu;
        }

        menu.ShowAt(MapControl, location, args.Position);
    }

    private void ViewModel_TrackChanged(Track oldTrack, Track newTrack)
    {
        _routingLayer.MapElements.Clear();

        if (oldTrack is not null)
        {
            _routeBuilderAdapter.Disconnect();
        }
        if (newTrack is not null)
        {
            _routeBuilderAdapter = new RouteBuilderAdapter(newTrack.RouteBuilder, _routingLayer, ViewModel);
        }
    }

    private void ViewModel_DragWayPointStarted(WayPoint wayPoint)
    {
        MapControl.Children.Add(_clickPanel);

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
        MapControl.Children.Remove(_clickPanel);
    }

    private void MapControl_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            ViewModel.CancelDragWayPointAsync().FireAndForget();
            e.Handled = true;
        }
    }
}
