using System;
using System.Threading.Tasks;
using CommunityToolkit.WinUI;
using cycloid.Routing;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace cycloid.Controls;

public sealed partial class Map : UserControl
{
    private MapTileSource _heatmap;
    private MapTileSource _osm;
    private MapElementsLayer _routingLayer;

    public Map()
    {
        InitializeComponent();
    }

    private ViewModel ViewModel => (ViewModel)this.FindResource(nameof(ViewModel));

    public async Task SetCenterAsync(string address, int zoom = 10)
    {
        Geopoint point = await ViewModel.GetLocationAsync(address, MapControl.Center);
        if (point is not null)
        {
            await MapControl.TrySetViewAsync(point, zoom, null, null, MapAnimationKind.Default);
        }
    }

    private bool IsFileSplit(WayPoint wayPoint)
    {
        return wayPoint is { IsFileSplit: true };
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
        ViewModel.DragWayPointStarting += ViewModel_DragWayPointStarting;
        ViewModel.DragNewWayPointStarting += ViewModel_DragNewWayPointStarting;
        ViewModel.DragWayPointStarted += ViewModel_DragWayPointStarted;
        ViewModel.DragWayPointEnded += ViewModel_DragWayPointEnded;

        MapControl.Center = new Geopoint(new BasicGeoposition() { Latitude = 46.46039124618558, Longitude = 10.089039490153148 });

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

    private void ThrottledClickPanelPointerMoved(PointerRoutedEventArgs e)
    {
        if (MapControl.TryGetLocationFromOffset(e.GetCurrentPoint(MapControl).Position, out Geopoint location))
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

    private void MapControl_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            ViewModel.CancelDragWayPointAsync().FireAndForget();
            e.Handled = true;
        }
    }

    private void ViewModel_TrackChanged(Track oldTrack, Track newTrack)
    {
        _routingLayer.MapElements.Clear();

        if (oldTrack is not null)
        {
            Disconnect(oldTrack);
        }
        if (newTrack is not null)
        {
            Connect(newTrack);
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
        PointerMovedEnabled = true;

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
        PointerMovedEnabled = false;
        EndDrag();
    }
}
