using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using cycloid.Info;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Input;

namespace cycloid.Controls;

public sealed partial class Map : ViewModelControl, INotifyPropertyChanged,
    IRecipient<ModeChanged>,
    IRecipient<TrackChanged>,
    IRecipient<HoverPointChanged>,
    IRecipient<CurrentPointChanged>,
    IRecipient<InfoVisibleChanged>,
    IRecipient<BringLocationIntoViewMessage>,
    IRecipient<BringTrackIntoViewMessage>,
    IRecipient<TrackComplete>
{
    private static readonly PropertyChangedEventArgs _centerChangedEventArgs = new(nameof(Center));
    private static readonly PropertyChangedEventArgs _headingChangedEventArgs = new(nameof(Heading));

    private readonly AsyncThrottle<MapPoint, Map> _loadInfosThrottle = new(
        static (value, @this, cancellationToken) => @this.LoadInfosAsync(value, cancellationToken),
        TimeSpan.FromSeconds(5));

    private MapTileSource _heatmap;
    private MapTileSource _osm;
    private MapElementsLayer _routingLayer;
    private MapElementsLayer _differenceLayer;
    private MapElementsLayer _poisLayer;
    private MapElementsLayer _infoLayer;
    private MapElementsLayer _trainLayer;
    private MapPolyline _selectionLine;

    private PropertyChangedEventHandler _propertyChanged;

    public Map()
    {
        InitializeComponent();

        MapControl.CenterChanged += (_, _) => _propertyChanged?.Invoke(this, _centerChangedEventArgs);
        MapControl.HeadingChanged += (_, _) => _propertyChanged?.Invoke(this, _headingChangedEventArgs);

        StrongReferenceMessenger.Default.Register<ModeChanged>(this);
        StrongReferenceMessenger.Default.Register<TrackChanged>(this);
        StrongReferenceMessenger.Default.Register<CurrentPointChanged>(this);
        StrongReferenceMessenger.Default.Register<HoverPointChanged>(this);
        StrongReferenceMessenger.Default.Register<InfoVisibleChanged>(this);
        StrongReferenceMessenger.Default.Register<BringLocationIntoViewMessage>(this);
        StrongReferenceMessenger.Default.Register<BringTrackIntoViewMessage>(this);
        StrongReferenceMessenger.Default.Register<TrackComplete>(this);

        RegisterRoutingMessages();
        RegisterCompareSessionMessages();
        RegisterPoisMessages();
    }

    public Geopoint Center => MapControl.Center;

    public float Heading => (float)MapControl.Heading;

    public void ZoomTrackDifference(TrackDifference difference)
    {
        MapPolyline differenceLine = GetDifferenceLine(difference);
        if (differenceLine is not null)
        {
            GeoboundingBox bounds = GeoboundingBox.TryCompute(differenceLine.Path.Positions);
            if (bounds is not null)
            {
                var zoomLevel = Math.Max(15, MapControl.ZoomLevel);
                SetViewAsync().FireAndForget();

                async Task SetViewAsync()
                {
                    await MapControl.TrySetViewBoundsAsync(bounds, new Thickness(25), MapAnimationKind.None);
                    await Task.Delay(10);
                    if (MapControl.ZoomLevel > zoomLevel)
                    {
                        MapControl.ZoomLevel = zoomLevel;
                    }
                }
            }
        }
    }

    private async Task SetCenterAsync(Geopoint point, float zoom = 12.9f, bool onlyIfNotInView = false)
    {
        if (onlyIfNotInView)
        {
            MapControl.IsLocationInView(point, out bool isInView);
            if (isInView)
            {
                return;
            }
        }

        zoom = MathF.Max(zoom, (float)MapControl.ZoomLevel);

        await MapControl.TrySetViewAsync(point, zoom, null, null, MapAnimationKind.Linear);
    }

    private async Task SetCenterAsync(IEnumerable<MapPoint> points)
    {
        GeoboundingBox bounds = GeoboundingBox.TryCompute(points.Select(trackPoint => trackPoint.ToBasicGeoposition()));
        if (bounds is not null)
        {
            await MapControl.TrySetViewBoundsAsync(bounds, new Thickness(ActualWidth * .05), MapAnimationKind.Bow);
        }
    }

    private void ShowMapMenu(Point position, MapPoint location)
    {
        MapMenuFlyout menu;
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
            HandlePoiPointerMoved(position);
            menu = MapOnTrackMenu;
        }
        else
        {
            menu = MapTrainMenu;
        }

        menu.ShowAt(MapControl, location, position);
    }

    private void Nudge()
    {
        MapControl.MapElements.Clear();
        MapControl.MapElements.Add(new MapIcon { MapStyleSheetEntry = "Dummy.Point", Location = MapControl.Center });
    }

    private async Task LoadInfosAsync(MapPoint location, CancellationToken cancellationToken)
    {
        ViewModel.InfoIsLoading = true;
        try
        {
            await ViewModel.Infos.LoadAsync(location, cancellationToken);
        }
        finally
        {
            ViewModel.InfoIsLoading = false;
        }
    }

    private void ViewModelControl_Loaded(object _1, RoutedEventArgs _2)
    {
        foreach (InfoCategory category in InfoCategory.All)
        {
            if (!category.Hide)
            {
                MenuFlyoutSubItem subItem = new() { Text = $"Add {category.Name.ToLower()} point" };
                foreach (InfoType type in category.Types)
                {
                    PointOfInterestCommandParameter parameter = new() { Type = type };
                    MapOnTrackMenu.RegisterPropertyChangedCallback(
                        MapMenuFlyout.LocationProperty, 
                        (sender, _) => parameter.Location = ((MapMenuFlyout)sender).Location);
                    subItem.Items.Add(new MenuFlyoutItem
                    {
                        Text = type.ToString(),
                        Command = ViewModel.AddPointOfInterestCommand,
                        CommandParameter = parameter,
                    });
                }
                MapOnTrackMenu.Items.Add(subItem);
            }
        }
    }

    private void MapControl_Loaded(object _1, RoutedEventArgs _2)
    {
        ViewModel.Sections.CollectionChanged += OnTracks_CollectionChanged;
        ViewModel.Points.CollectionChanged += OnTracks_CollectionChanged;

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

        _trainLayer = (MapElementsLayer)MapControl.Resources["TrainLayer"];
        MapControl.Layers.Add(_trainLayer);

        MapControl.ActualCameraChanging += MapControl_ActualCameraChanging;

        //XAsync().FireAndForget();

        //async Task XAsync()
        //{
        //    MapModel3D x = await MapModel3D.CreateFrom3MFAsync(RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/cycle.3mf")));
        //    MapElement3D y = new() { Model = x, Location = new Geopoint(new BasicGeoposition { Latitude = 47.76002, Longitude = 12.21696 }), Scale = new(.1f, .1f, .1f), Heading = 45 };
        //    _trainLayer.MapElements.Add(y);
        //    await Task.Delay(TimeSpan.FromSeconds(20));
        //    while (true)
        //    {
        //        y.Heading += 0.1;
        //        y.Location = new Geopoint(new BasicGeoposition { Latitude = y.Location.Position.Latitude + .00001, Longitude = y.Location.Position.Longitude });
        //        await Task.Delay(TimeSpan.FromMilliseconds(20));
        //    }
        //}
    }

    private void MapControl_ActualCameraChanging(MapControl _1, MapActualCameraChangingEventArgs args)
    {
        if (ViewModel.InfoVisible)
        {
            _loadInfosThrottle.Next(args.Camera.Location.Position.ToMapPoint(), this);
        }
    }

    private void MapControl_PreviewKeyDown(object _, KeyRoutedEventArgs e)
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
            ViewModel.AddDestinationAsync(args.Location.Position.ToMapPoint()).FireAndForget();
        }
    }

    private void MapControl_MapContextRequested(MapControl _, MapContextRequestedEventArgs args)
    {
        ShowMapMenu(args.Position, args.Location.Position.ToMapPoint());
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
        else
        {
            HandlePoiTapped();
        }

        e.Handled = true;
    }

    private void PointerPanel_PointerMoved(object _, PointerRoutedEventArgs e)
    {
        PointerPoint pointer = e.GetCurrentPoint(MapControl);
        Point position = pointer.Position;
        if (ViewModel.Mode == Modes.Edit)
        {
            HandleRoutingPointerMoved(position);
        }
        else if (ViewModel.Mode == Modes.Train)
        {
            HandleTrainingPointerMoved(pointer);
        }
        else
        {
            HandlePoiPointerMoved(position);
        }

        e.Handled = true;
    }

    private void PointerPanel_ContextRequested(UIElement _, ContextRequestedEventArgs args)
    {
        if (args.TryGetPosition(MapControl, out Point position) && 
            MapControl.TryGetLocationFromOffset(position, out Geopoint location) &&
            !(ViewModel.IsPlaying && _pointerPanelPointerMoved))
        {
            ShowMapMenu(position, location.Position.ToMapPoint());
        }
    }

    event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }

    void IRecipient<ModeChanged>.Receive(ModeChanged message)
    {
        bool isEditMode = message.NewValue == Modes.Edit;
        if (isEditMode != (message.OldValue == Modes.Edit))
        {
            foreach (MapIcon icon in _routingLayer.MapElements.OfType<MapIcon>())
            {
                icon.Visible = isEditMode;
            }
            PointerPanel.IsEnabled = !isEditMode;
        }
    }

    void IRecipient<TrackChanged>.Receive(TrackChanged message)
    {
        if (message.OldValue is not null)
        {
            _routingLayer.MapElements.Clear();
            if (message.OldValue.CompareSession is not null)
            {
                message.OldValue.CompareSession.Differences.CollectionChanged -= Differences_CollectionChanged;
                _differenceLayer.MapElements.Clear();
            }
        }

        DisconnectRouting(message.OldValue);
        ConnectRouting(message.NewValue);
    }

    void IRecipient<HoverPointChanged>.Receive(HoverPointChanged message)
    {
        Nudge();
    }

    void IRecipient<InfoVisibleChanged>.Receive(InfoVisibleChanged message)
    {
        if (message.Value)
        {
            _loadInfosThrottle.Next(MapControl.ActualCamera.Location.Position.ToMapPoint(), this);
        }
    }

    void IRecipient<BringLocationIntoViewMessage>.Receive(BringLocationIntoViewMessage message)
    {
        SetCenterAsync(new Geopoint(message.Value.ToBasicGeoposition())).FireAndForget();
    }

    void IRecipient<BringTrackIntoViewMessage>.Receive(BringTrackIntoViewMessage message)
    {
        if (!message.Value.Item2.IsValid)
        {
            SetCenterAsync(new Geopoint(message.Value.Item1.ToBasicGeoposition())).FireAndForget();
        }
        else
        {
            float distance1 = message.Value.Item1.Distance;
            float distance2 = message.Value.Item2.Distance;
            SetCenterAsync(ViewModel.Track.Points.Enumerate(Math.Min(distance1, distance2), Math.Max(distance1, distance2)).Select(p => p.Location)).FireAndForget();
        }
    }

    void IRecipient<TrackComplete>.Receive(TrackComplete message)
    {
        if (!message.IsNew)
        {
            SetCenterAsync(ViewModel.Track.Points.Select(p => (MapPoint)p)).FireAndForget();
        }
    }
}
