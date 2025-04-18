using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using cycloid.Info;
using cycloid.Routing;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;

namespace cycloid.Controls;

partial class Map : 
    IRecipient<HoverInfoChanged>,
    IRecipient<InfoCategoryVisibleChanged>,
    IRecipient<InfosActivated>,
    IRecipient<InfosDeactivated>
{
    private readonly Throttle<(Point, MapPoint), Map> _hoverElementThrottle = new(
        static (value, @this) => @this.HoverElement(value),
        TimeSpan.FromMilliseconds(70));

    private void RegisterPoisMessages()
    {
        StrongReferenceMessenger.Default.Register<HoverInfoChanged>(this);
        StrongReferenceMessenger.Default.Register<InfoCategoryVisibleChanged>(this);
        StrongReferenceMessenger.Default.Register<InfosActivated>(this);
        StrongReferenceMessenger.Default.Register<InfosDeactivated>(this);
    }

    private MapPoint? GetLocation(Point offset) 
        => MapControl.TryGetLocationFromOffset(offset, out Geopoint location) ? location.Position.ToMapPoint() : null;

    private MapIcon GetOnTrackIcon(OnTrack onTrack) 
        => _poisLayer.MapElements.OfType<MapIcon>().FirstOrDefault(element => (OnTrack)element.Tag == onTrack);

    private MapIcon GetInfoIcon(InfoPoint info) 
        => _infoLayer.MapElements.OfType<MapIcon>().FirstOrDefault(element => (InfoPoint)element.Tag == info);

    private void OnTracks_CollectionChanged(object _, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                {
                    OnTrack onTrack = (OnTrack)e.NewItems[0];
                    _poisLayer.MapElements.Add(new MapIcon
                    {
                        Location = new Geopoint(onTrack.Location.ToBasicGeoposition()),
                        MapStyleSheetEntry = $"POI.{onTrack.PointOfInterest.Type}",
                        Title = onTrack.Name ?? "",
                        Tag = onTrack
                    });
                    onTrack.PropertyChanged += OnTrack_PropertyChanged;
                }
                break;
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Remove:
                {
                    OnTrack onTrack = (OnTrack)e.OldItems[0];
                    onTrack.PropertyChanged -= OnTrack_PropertyChanged;
                    _poisLayer.MapElements.Remove(GetOnTrackIcon(onTrack));
                }
                break;
            case NotifyCollectionChangedAction.Replace:
                break;
            case NotifyCollectionChangedAction.Reset:
                {
                    foreach (OnTrack onTrack in _poisLayer.MapElements.Select(element => (OnTrack)element.Tag))
                    {
                        onTrack.PropertyChanged -= OnTrack_PropertyChanged;
                    }
                    _poisLayer.MapElements.Clear();
                }
                break;
        }
    }

    private void OnTrack_PropertyChanged(object sender, PropertyChangedEventArgs _)
    {
        OnTrack onTrack = (OnTrack)sender;
        GetOnTrackIcon(onTrack).Title = onTrack.Name ?? "";
    }

    private void HandlePoiTapped()
    {
        ViewModel.CurrentPoint = ViewModel.HoverPoint;
    }

    private void HandlePoiPointerMoved(Point offset)
    {
        MapPoint? tryLocation = GetLocation(offset);
        if (tryLocation is MapPoint location)
        {
            _hoverElementThrottle.Next((offset, location), this);
        }
    }

    private void HoverElement((Point Offset, MapPoint Location) value)
    { 
        IReadOnlyList<MapElement> elements = MapControl.FindMapElementsAtOffset(value.Offset, 7);

        MapIcon nearestIcon = elements
            .Where(element => element.Tag is InfoPoint)
            .Cast<MapIcon>()
            .MinBy(element => GeoCalculation.Distance(element.Location.Position.ToMapPoint(), value.Location));

        InfoPoint nearestInfo = nearestIcon?.Tag as InfoPoint ?? InfoPoint.Invalid;
        if (nearestInfo != ViewModel.HoverInfo)
        {
            if (ViewModel.HoverInfo.IsValid)
            {
                MapIcon icon = GetInfoIcon(ViewModel.HoverInfo);
                if (icon is not null)
                {
                    icon.MapStyleSheetEntryState = "";
                    icon.ZIndex = 0;
                }
            }
            if (nearestIcon is not null)
            {
                nearestIcon.MapStyleSheetEntryState = "Info.hover";
                nearestIcon.ZIndex = 100;
            }
            ViewModel.HoverInfo = nearestInfo;
        }

        (MapPoint, MapPoint) boundingBox = GetBoundingBox();
        ViewModel.HoverPoint = elements
            .Where(element => element.Tag is RouteSection)
            .Select(element => ViewModel.Track.Points.GetNearestPoint(value.Location, boundingBox, (RouteSection)element.Tag))
            .MinBy(((TrackPoint Point, float Distance) nearest) => nearest.Distance, defaultValue: (TrackPoint.Invalid, default))
            .Point;

        (MapPoint NorthWest, MapPoint SouthEast) GetBoundingBox()
        {
            GeoboundingBox box = GeoboundingBox.TryCompute(MapControl.GetVisibleRegion(MapVisibleRegionKind.Near).Positions);

            return (box.NorthwestCorner.ToMapPoint(), box.SoutheastCorner.ToMapPoint());
        }
    }

    void IRecipient<HoverInfoChanged>.Receive(HoverInfoChanged message)
    {
        if (message.Value.IsValid)
        {
            string name = message.Value.Name;
            if (name.Length > 16)
            {
                name = name[..14] + "... ";
            }
            else if (name.Length > 0)
            {
                name += " ";
            }
            ConvertInfoMenuItem.Text = $"Add {name}as {message.Value.Category.Name.ToLower()} point ({message.Value.Type})";
        }

        Visibility visibility = message.Value.IsValid ? Visibility.Collapsed : Visibility.Visible;
        foreach (MenuFlyoutSubItem menuItem in MapOnTrackMenu.Items.OfType<MenuFlyoutSubItem>())
        {
            menuItem.Visibility = visibility;
        }
    }

    void IRecipient<InfoCategoryVisibleChanged>.Receive(InfoCategoryVisibleChanged message)
    {
        IEnumerable<MapIcon> icons = (message.Pois ? _poisLayer : _infoLayer).MapElements.Cast<MapIcon>();
        if (message.Category is not null)
        {
            if (message.Pois)
            {
                icons = icons.Where(icon => ((OnTrack)icon.Tag).PointOfInterest.Category == message.Category);
            }
            else
            {
                icons = icons.Where(icon => ((InfoPoint)icon.Tag).Category == message.Category);
            }
        }

        foreach (MapIcon icon in icons)
        {
            icon.Visible = message.NewValue;
        }
    }

    void IRecipient<InfosActivated>.Receive(InfosActivated message)
    {
        foreach (InfoPoint infoPoint in message.Infos)
        {
            _infoLayer.MapElements.Add(new MapIcon
            {
                Location = new Geopoint(infoPoint.Location.ToBasicGeoposition()),
                MapStyleSheetEntry = $"Info.{infoPoint.Type}",
                Title = infoPoint.Name,
                Tag = infoPoint,
                Visible = ViewModel.GetInfoCategoryVisible(false, infoPoint.Category),
            });
        }
    }

    void IRecipient<InfosDeactivated>.Receive(InfosDeactivated message)
    {
        int count = message.Count;
        while (count-- > 0)
        {
            _infoLayer.MapElements.RemoveAt(message.Index);
        }
    }
}