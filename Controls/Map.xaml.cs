using System;
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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Input;

namespace cycloid.Controls;

public sealed partial class Map : ViewModelControl, INotifyPropertyChanged,
    IRecipient<ModeChanged>,
    IRecipient<TrackChanged>,
    IRecipient<HoverPointChanged>,
    IRecipient<InfoVisibleChanged>,
    IRecipient<SetMapCenterMessage>,
    IRecipient<TrackComplete>
{
    private static PropertyChangedEventArgs _centerChangedEventArgs = new PropertyChangedEventArgs(nameof(Center));
    private static PropertyChangedEventArgs _headingChangedEventArgs = new PropertyChangedEventArgs(nameof(Heading));

    private readonly AsyncThrottle<object, Map> _loadInfosThrottle = new(
        static (_, @this, cancellationToken) => @this.LoadInfosAsync(cancellationToken),
        TimeSpan.FromSeconds(10));

    private MapTileSource _heatmap;
    private MapTileSource _osm;
    private MapElementsLayer _routingLayer;
    private MapElementsLayer _differenceLayer;
    private MapElementsLayer _poisLayer;
    private MapElementsLayer _infoLayer;
    private MapPolyline _selectionLine;
    private MapIcon _dummyIcon;

    private PropertyChangedEventHandler _propertyChanged;

    public Map()
    {
        InitializeComponent();

        MapControl.CenterChanged += (_, _) => _propertyChanged?.Invoke(this, _centerChangedEventArgs);
        MapControl.HeadingChanged += (_, _) => _propertyChanged?.Invoke(this, _headingChangedEventArgs);

        StrongReferenceMessenger.Default.Register<ModeChanged>(this);
        StrongReferenceMessenger.Default.Register<TrackChanged>(this);
        StrongReferenceMessenger.Default.Register<HoverPointChanged>(this);
        StrongReferenceMessenger.Default.Register<InfoVisibleChanged>(this);
        StrongReferenceMessenger.Default.Register<SetMapCenterMessage>(this);
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
                SetViewAsync().FireAndForget();

                async Task SetViewAsync()
                {
                    await MapControl.TrySetViewBoundsAsync(bounds, new Thickness(25), MapAnimationKind.None);
                    await Task.Delay(10);
                    if (MapControl.ZoomLevel > 15)
                    {
                        MapControl.ZoomLevel = 15;
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

    // WORKAROUND Change the view slightly to update moved child controls.
    private void Nudge()
    {
        if (_dummyIcon is null)
        {
            MapControl.MapElements.Add(_dummyIcon = new MapIcon { MapStyleSheetEntry = "Dummy.Point", Location = MapControl.Center });
        }
        else
        {
            MapControl.MapElements.Clear();
            _dummyIcon = null;
        }
    }

    private async Task LoadInfosAsync(CancellationToken cancellationToken)
    {
        ViewModel.InfoIsLoading = true;
        try
        {
            await ViewModel.Infos.LoadAsync((MapPoint)MapControl.ActualCamera.Location.Position, cancellationToken);
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
        ShowMapMenu(args.Position, (MapPoint)args.Location.Position);
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
        Point position = e.GetCurrentPoint(MapControl).Position;
        if (ViewModel.Mode == Modes.Edit)
        {
            HandleRoutingPointerMoved(position);
        }
        else
        {
            HandlePoiPointerMoved(position);
        }

        e.Handled = true;
    }

    private void PointerPanel_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (args.TryGetPosition(MapControl, out Point position) && 
            MapControl.TryGetLocationFromOffset(position, out Geopoint location))
        {
            ShowMapMenu(position, (MapPoint)location.Position);
        }
    }

    event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
    {
        add
        {
            _propertyChanged += value;
        }

        remove
        {
            _propertyChanged -= value;
        }
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
        _routingLayer.MapElements.Clear();

        DisconnectRouting(message.OldValue);
        ConnectRouting(message.NewValue);
    }

    void IRecipient<HoverPointChanged>.Receive(HoverPointChanged message)
    {
        Nudge();
    }

    void IRecipient<InfoVisibleChanged>.Receive(InfoVisibleChanged message)
    {
        if (message.NewValue)
        {
            _loadInfosThrottle.Next(null, this);
        }
    }

    void IRecipient<SetMapCenterMessage>.Receive(SetMapCenterMessage message)
    {
        SetCenterAsync(new Geopoint(message.Value)).FireAndForget();
    }

    void IRecipient<TrackComplete>.Receive(TrackComplete message)
    {
        if (!message.IsNew)
        {
            GeoboundingBox bounds = GeoboundingBox.TryCompute(ViewModel.Track.Points.Select(trackPoint => (BasicGeoposition)trackPoint));
            if (bounds is not null)
            {
                MapControl.TrySetViewBoundsAsync(bounds, new Thickness(25), MapAnimationKind.Bow).AsTask().FireAndForget();
            }
        }
    }
}
