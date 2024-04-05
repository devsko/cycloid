using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.WinUI;
using cycloid.Info;
using Windows.Devices.Geolocation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Input;

namespace cycloid.Controls;

public sealed partial class Map : UserControl
{
    private readonly Throttle<object, Map> _loadInfosThrottle = new(
        static (_, @this, cancellationToken) => @this.LoadInfosAsync(cancellationToken),
        TimeSpan.FromSeconds(2));

    private MapTileSource _heatmap;
    private MapTileSource _osm;
    private MapElementsLayer _routingLayer;
    private MapElementsLayer _differenceLayer;
    private MapElementsLayer _poisLayer;
    private MapElementsLayer _infoLayer;

    public Map()
    {
        InitializeComponent();
    }

    private ViewModel ViewModel => (ViewModel)this.FindResource(nameof(ViewModel));

    public Geopoint Center => MapControl.Center;

    public async Task SetCenterAsync(string address, int zoom = 12)
    {
        Geopoint point = await ViewModel.GetLocationAsync(address, MapControl.Center);
        if (point is not null)
        {
            await MapControl.TrySetViewAsync(point, zoom, null, null, MapAnimationKind.Bow);
        }
    }

    public async Task SetCenterAsync(Geopoint point, int zoom = 12)
    {
        await MapControl.TrySetViewAsync(point, zoom, null, null, MapAnimationKind.Bow);
    }

    public void ZoomTrackDifference(TrackDifference difference)
    {
        MapPolyline differenceLine = GetDifferenceLine(difference);
        if (differenceLine is not null)
        {
            GeoboundingBox bounds = GeoboundingBox.TryCompute(differenceLine.Path.Positions);
            if (bounds is not null)
            {
                MapControl.TrySetViewBoundsAsync(bounds, new Thickness(25), MapAnimationKind.Linear).AsTask().FireAndForget();
            }
        }
    }

    // WORKAROUND Change the view slightly to update moved child controls.
    private void Nudge()
    {
        if (IsEqualCamera(MapControl.ActualCamera, MapControl.TargetCamera))
        {
            MapCamera camera = MapControl.TargetCamera;
            camera.Roll = camera.Roll == 0 ? 1e-5 : 0;
            _ = MapControl.TrySetSceneAsync(MapScene.CreateFromCamera(camera), MapAnimationKind.None);
        }

        static bool IsEqualCamera(MapCamera camera1, MapCamera camera2) =>
            IsEqualDouble(camera1.Location.Position.Latitude, camera2.Location.Position.Latitude) &&
            IsEqualDouble(camera1.Location.Position.Longitude, camera2.Location.Position.Longitude) &&
            (IsEqualDouble(camera1.Heading, camera2.Heading) || Math.Abs(Math.Abs(camera1.Heading - camera2.Heading) - 360) < 1e-4) &&
            IsEqualDouble(camera1.FieldOfView, camera2.FieldOfView) &&
            IsEqualDouble(camera1.Pitch, camera2.Pitch) &&
            IsEqualDouble(camera1.Roll, camera2.Roll);

        static bool IsEqualDouble(double value1, double value2)
            => Math.Abs(value1 - value2) < 1e-4;
    }

    private async Task LoadInfosAsync(CancellationToken _)
    {
        ViewModel.InfoIsLoading = true;
        try
        {
            await ViewModel.Infos.SetCenterAsync((MapPoint)MapControl.ActualCamera.Location.Position);
        }
        finally
        {
            ViewModel.InfoIsLoading = false;
        }
    }

    private void MapControl_Loaded(object _1, RoutedEventArgs _2)
    {
        ViewModel.ModeChanged += ViewModel_ModeChanged;
        ViewModel.InfoVisibleChanged += ViewModel_InfoVisibleChanged;
        ViewModel.TrackChanged += ViewModel_TrackChanged;
        ViewModel.CompareSessionChanged += ViewModel_CompareSessionChanged;
        ViewModel.HoverPointChanged += ViewModel_HoverPointChanged;
        ViewModel.DragWayPointStarting += ViewModel_DragWayPointStarting;
        ViewModel.DragNewWayPointStarting += ViewModel_DragNewWayPointStarting;
        ViewModel.DragWayPointStarted += ViewModel_DragWayPointStarted;
        ViewModel.DragWayPointEnded += ViewModel_DragWayPointEnded;
        ViewModel.InfoCategoryVisibleChanged += ViewModel_InfoCategoryVisibleChanged;
        ViewModel.Infos.InfosActivated += Infos_InfosActivated;
        ViewModel.Infos.InfosDeactivated += Infos_InfosDeactivated;
        ViewModel.Sections.CollectionChanged += Sections_CollectionChanged;
        ViewModel.Points.CollectionChanged += Points_CollectionChanged;

        MapControl.Center = new Geopoint(new BasicGeoposition() { Latitude = 46.46039124618558, Longitude = 10.089039490153148 });

        _heatmap = (MapTileSource)MapControl.Resources["Heatmap"];
        MapControl.Resources.Remove("Heatmap");
        MapControl.TileSources.Add(_heatmap);

        _osm = (MapTileSource)MapControl.Resources["Osm"];
        MapControl.Resources.Remove("Osm");
        MapControl.TileSources.Add(_osm);

        _poisLayer = (MapElementsLayer)MapControl.Resources["PoisLayer"];
        MapControl.Layers.Add(_poisLayer);

        _infoLayer = (MapElementsLayer)MapControl.Resources["InfoLayer"];
        MapControl.Layers.Add(_infoLayer);

        _differenceLayer = (MapElementsLayer)MapControl.Resources["DifferenceLayer"];
        MapControl.Layers.Add(_differenceLayer);

        _routingLayer = (MapElementsLayer)MapControl.Resources["RoutingLayer"];
        MapControl.Layers.Add(_routingLayer);

        foreach (InfoCategory category in InfoCategory.All.Where(c => !c.Hide))
        {
            MenuFlyoutSubItem subItem = new() { Text = category.Name };
            foreach (InfoType type in category.Types)
            {
                PointOfInterestCommandParameter parameter = new() { Type = type };
                MapPointMenu.RegisterPropertyChangedCallback(MapMenuFlyout.LocationProperty, (sender, _) => parameter.Location = ((MapMenuFlyout)sender).Location);
                subItem.Items.Add(new MenuFlyoutItem
                {
                    Text = type.ToString(),
                    Command = ViewModel.AddPointOfInterestCommand,
                    CommandParameter = parameter,
                });
            }
            AddPoiButton.Items.Add(subItem);
        }
    }

    private void MapControl_ActualCameraChanged(MapControl _1, MapActualCameraChangedEventArgs _2)
    {
        if (ViewModel.InfoVisible)
        {
            _loadInfosThrottle.Next(null, this);
        }
    }

    private void MapControl_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel.Mode == Modes.Edit)
        {
            if (HandleRoutingKey(e.Key))
            {
                e.Handled = true;
            }
        }
    }

    private void MapControl_MapTapped(MapControl _, MapInputEventArgs args)
    {
        if (ViewModel.Mode != Modes.Edit)
        {
            return;
        }

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
        MapMenuFlyout menu;
        MapPoint location = (MapPoint)args.Location.Position;
        if (ViewModel.Track is null)
        {
            menu = MapNoTrackMenu;
        }
        else if (ViewModel.Mode == Modes.Edit)
        {
            if (ViewModel.HoveredWayPoint is not null)
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
        }
        else if (ViewModel.Mode is Modes.Sections or Modes.POIs)
        {
            menu = MapPointMenu;
        }
        else
        {
            menu = MapTrainMenu;
        }

        menu.ShowAt(MapControl, location, args.Position);
    }

    private void MapControl_LosingFocus(UIElement _, LosingFocusEventArgs args)
    {
        // WORKAROUND: Focus gets lost (to root scroller) when paning the map

        if (ViewModel.IsCaptured)
        {
            args.TryCancel();
        }
    }

    private void PointerPanel_Tapped(object _, TappedRoutedEventArgs e)
    {
        if (ViewModel.Mode == Modes.Edit)
        {
            HandleRoutingPanelTapped();
        }

        e.Handled = true;
    }

    private void PointerPanel_PointerMoved(object _, PointerRoutedEventArgs e)
    {
        if (ViewModel.Mode == Modes.Edit)
        {
            HandleRoutingPanelPointerMoved(e.GetCurrentPoint(MapControl).Position);
        }

        e.Handled = true;
    }

    private void ViewModel_ModeChanged(Modes oldMode, Modes newMode)
    {
        bool isEditMode = newMode == Modes.Edit;
        if (isEditMode != (oldMode == Modes.Edit))
        {
            foreach (MapElement element in _routingLayer.MapElements)
            {
                if (element is MapIcon)
                {
                    element.Visible = isEditMode;
                }
                else
                {
                    element.IsEnabled = isEditMode;
                }
            }
        }
    }

    private void ViewModel_InfoVisibleChanged(bool oldVisible, bool newVisible)
    {
        if (newVisible)
        {
            _loadInfosThrottle.Next(null, this);
        }
    }

    private void ViewModel_TrackChanged(Track oldTrack, Track newTrack)
    {
        _routingLayer.MapElements.Clear();

        if (oldTrack is not null)
        {
            DisconnectRouting(oldTrack);
        }
        if (newTrack is not null)
        {
            ConnectRouting(newTrack);
        }
    }

    private void ViewModel_HoverPointChanged(TrackPoint arg1, TrackPoint arg2)
    {
        Nudge();
    }

    private void ViewModel_InfoCategoryVisibleChanged(InfoCategory category, bool value)
    {
        IEnumerable<MapIcon> icons = _infoLayer.MapElements.Cast<MapIcon>();
        if (category is not null)
        {
            icons = icons.Where(icon => icon.Tag.Equals(category));
        }

        foreach (MapIcon icon in icons)
        {
            icon.Visible = value;
        }
    }

    private void Infos_InfosActivated(InfoPoint[] infoPoints)
    {
        foreach (InfoPoint infoPoint in infoPoints)
        {
            _infoLayer.MapElements.Add(new MapIcon
            {
                Location = new Geopoint(infoPoint.Location),
                MapStyleSheetEntry = $"Info.{infoPoint.Type}",
                Title = infoPoint.Name,
                Tag = infoPoint.Category,
                Visible = ViewModel.GetInfoCategoryVisible(infoPoint.Category),
            });
        }
    }

    private void Infos_InfosDeactivated(int startIndex, int length)
    {
        while (length-- > 0)
        {
            _infoLayer.MapElements.RemoveAt(startIndex);
        }
    }
}
