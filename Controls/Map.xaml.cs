using CommunityToolkit.WinUI;
using cycloid.Routing;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
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

    private readonly ClickPanel _clickPanel;
    private MapTileSource _heatmap;
    private MapTileSource _osm;
    private MapElementsLayer _routingLayer;
    private MapPolyline _cachedline;

    public WayPoint HoveredWayPoint
    {
        get => (WayPoint)GetValue(HoveredWayPointProperty);
        set => SetValue(HoveredWayPointProperty, value);
    }

    public static readonly DependencyProperty HoveredWayPointProperty =
        DependencyProperty.Register(nameof(HoveredWayPoint), typeof(WayPoint), typeof(Map), new PropertyMetadata(null));

    public RouteSection HoveredSection
    {
        get => (RouteSection)GetValue(HoveredSectionProperty);
        set => SetValue(HoveredSectionProperty, value);
    }

    public static readonly DependencyProperty HoveredSectionProperty =
        DependencyProperty.Register(nameof(HoveredSection), typeof(RouteSection), typeof(Map), new PropertyMetadata(null));

    public Map()
    {
        InitializeComponent();

        _clickPanel = new ClickPanel();
        _clickPanel.PointerMoved += ClickPanel_PointerMoved;
        _clickPanel.Tapped += ClickPanel_Tapped;
    }

    private ViewModel ViewModel => (ViewModel)this.FindResource(nameof(ViewModel));

    private void MapControl_Loaded(object _1, RoutedEventArgs _2)
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

    private void RoutingLayer_MapElementPointerExited(MapElementsLayer _, MapElementsLayerPointerExitedEventArgs args)
    {
        if (args.MapElement is MapIcon { Tag: WayPoint wayPoint })
        {
            System.Diagnostics.Debug.Assert(wayPoint == HoveredWayPoint || (HoveredWayPoint is null && !args.MapElement.Visible));

            args.MapElement.MapStyleSheetEntryState = "";
        }
        else if (args.MapElement is MapPolyline { Tag: RouteSection section })
        {
            System.Diagnostics.Debug.Assert(section == HoveredSection || (HoveredSection is null && !args.MapElement.Visible));

            args.MapElement.MapStyleSheetEntry = "Routing.Line";
        }

        HoveredWayPoint = null;
        HoveredSection = null;
    }

    private void RoutingLayer_MapElementPointerEntered(MapElementsLayer _, MapElementsLayerPointerEnteredEventArgs args)
    {
        System.Diagnostics.Debug.Assert(HoveredWayPoint is null && HoveredSection is null);

        if (args.MapElement is MapIcon { Tag: WayPoint wayPoint})
        {
            args.MapElement.MapStyleSheetEntryState = "Routing.hover";
            HoveredWayPoint = wayPoint;
        }
        else if (args.MapElement is MapPolyline { Tag: RouteSection section })
        {
            args.MapElement.MapStyleSheetEntry = "Routing.HoveredLine";
            HoveredSection = section;
        }
    }

    private void RoutingLayer_MapElementClick(MapElementsLayer _, MapElementsLayerClickEventArgs args)
    {
        // No way to find out if it is a middle button click for delete. Use Ctrl-Click as workaround

        bool ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);

        if (!ViewModel.IsCaptured)
        {
            if (args.MapElements.OfType<MapIcon>().FirstOrDefault() is MapIcon { Tag: WayPoint wayPoint })
            {
                if (ctrl)
                {
                    ViewModel.DeleteRoutePoint(wayPoint);
                }
                else
                {
                    ViewModel.StartDrag(wayPoint);
                }
            }
            else if (!ctrl && args.MapElements.OfType<MapPolyline>().FirstOrDefault() is MapPolyline { Tag: RouteSection section })
            {
                ViewModel.StartDrag(section, new WayPoint((MapPoint)args.Location.Position, section.IsDirectRoute));
            }

            if (ViewModel.IsCaptured)
            {
                MapControl.Children.Add(_clickPanel);
            }
        }
    }

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

    private void ClickPanel_Tapped(object _, TappedRoutedEventArgs e)
    {
        ViewModel.EndDrag();
        MapControl.Children.Remove(_clickPanel);
        e.Handled = true;
    }

    private readonly Throttle<Point, Map> _pointerMovedThrottle = new(static (point, map) =>
    {
        if (map.MapControl.TryGetLocationFromOffset(point, out Geopoint location))
        {
            map.ViewModel.ContinueDrag((MapPoint)location.Position);
        }
        return Task.CompletedTask;
    }, TimeSpan.FromMilliseconds(100));

    private void ClickPanel_PointerMoved(object _, PointerRoutedEventArgs e)
    {
        _pointerMovedThrottle.Next(e.GetCurrentPoint(MapControl).Position, this);
        e.Handled = true;
    }

    private void MapControl_MapTapped(MapControl _, MapInputEventArgs args)
    {
        // This is raised ADDITIONALY to MapElementClick!
        if (HoveredWayPoint is not null || HoveredSection is not null)
        {
            return;
        }

        bool ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);

        if (!ctrl)
        {
            ViewModel.AddDestination(new WayPoint((MapPoint)args.Location.Position, false));
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
        else if (HoveredWayPoint is not null)
        {
            menu = MapEditPointMenu;
            location = HoveredWayPoint.Location;
        }
        else if (HoveredSection is not null)
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

        void RouteBuilderPoints_CollectionChanged(object _, NotifyCollectionChangedEventArgs e)
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
                        if (HoveredWayPoint == wayPoint)
                        {
                            HoveredWayPoint = null;
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
                        if (HoveredWayPoint == oldPoint)
                        {
                            HoveredWayPoint = newPoint;
                        }
                        MapIcon icon = GetWayPointIcon(oldPoint);
                        icon.Location = new Geopoint((BasicGeoposition)newPoint.Location);
                        icon.Tag = newPoint;
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    break;
            }

            MapIcon GetWayPointIcon(WayPoint point) => _routingLayer.MapElements.OfType<MapIcon>().First(element => (WayPoint)element.Tag == point);
        }

        void RouteBuilder_SectionAdded(RouteSection section, int index)
        {
            Geopath path = new([(BasicGeoposition)section.Start.Location, (BasicGeoposition)section.End.Location]);

            if (_cachedline is MapPolyline line)
            {
                _cachedline = null;
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

        void RouteBuilder_SectionRemoved(RouteSection section, int index)
        {
            MapPolyline line = GetSectionLine(section);
            if (section == HoveredSection)
            {
                HoveredSection = null;
            }
            line.Visible = false;
            if (_cachedline is null)
            {
                _cachedline = line;
            }
            else
            {
                _routingLayer.MapElements.Remove(line);
            }
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

        MapPolyline GetSectionLine(RouteSection section) => _routingLayer.MapElements.OfType<MapPolyline>().First(line => (RouteSection)line.Tag == section);
    }
}
