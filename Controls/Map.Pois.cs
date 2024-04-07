using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using cycloid.Info;
using cycloid.Routing;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI.Xaml.Controls.Maps;

namespace cycloid.Controls;

partial class Map
{
    private MapPoint? GetLocation(Point offset) 
        => MapControl.TryGetLocationFromOffset(offset, out Geopoint location) ? (MapPoint)location.Position : null;

    private MapIcon GetOnTrackIcon(OnTrack onTrack) 
        => _poisLayer.MapElements.OfType<MapIcon>().FirstOrDefault(element => (OnTrack)element.Tag == onTrack);

    private MapIcon GetInfoIcon(InfoPoint info) 
        => _infoLayer.MapElements.OfType<MapIcon>().FirstOrDefault(element => (InfoPoint)element.Tag == info);

    private void Points_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        OnTrackCollectionChanged(e);
    }

    private void Sections_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        OnTrackCollectionChanged(e);
    }

    private void OnTrackCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                {
                    OnTrack onTrack = (OnTrack)e.NewItems[0];
                    _poisLayer.MapElements.Add(new MapIcon
                    {
                        Location = new Geopoint(onTrack.PointOfInterest.IsSection ? onTrack.TrackPoint : onTrack.PointOfInterest.Location),
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

    private void HandlePoiPointerMoved(Point offset)
    {
        MapPoint? tryLocation = GetLocation(offset);
        if (tryLocation is not MapPoint location)
        {
            return;
        }

        IReadOnlyList<MapElement> elements = MapControl.FindMapElementsAtOffset(offset, 7);

        (MapIcon nearestIcon, float distance) = elements
            .Where(element => element.Tag is InfoPoint)
            .Cast<MapIcon>()
            .MinBy(element => GeoCalculation.Distance((MapPoint)element.Location.Position, location));

        InfoPoint nearestInfo = nearestIcon?.Tag as InfoPoint ?? InfoPoint.Invalid;
        if (nearestInfo != ViewModel.HoverInfo)
        {
            if (ViewModel.HoverInfo.IsValid)
            {
                MapIcon icon = GetInfoIcon(ViewModel.HoverInfo);
                icon.MapStyleSheetEntryState = "";
                icon.ZIndex = 0;
            }
            if (nearestIcon is not null)
            {
                nearestIcon.MapStyleSheetEntryState = "Info.hover";
                nearestIcon.ZIndex = 100;
            }
            ViewModel.HoverInfo = nearestInfo;
        }

        ViewModel.HoverPoint = 
            elements.Any(element => element.Tag is RouteSection) 
            ? ViewModel.Track.Points.GetNearestPoint(location)
            : TrackPoint.Invalid;
    }

    private void ViewModel_PoisCategoryVisibleChanged(InfoCategory category, bool value)
    {
        IEnumerable<MapIcon> icons = _poisLayer.MapElements.Cast<MapIcon>();
        if (category is not null)
        {
            icons = icons.Where(icon => ((OnTrack)icon.Tag).PointOfInterest.Category == category);
        }

        foreach (MapIcon icon in icons)
        {
            icon.Visible = value;
        }
    }

    private void ViewModel_InfoCategoryVisibleChanged(InfoCategory category, bool value)
    {
        IEnumerable<MapIcon> icons = _infoLayer.MapElements.Cast<MapIcon>();
        if (category is not null)
        {
            icons = icons.Where(icon => ((InfoPoint)icon.Tag).Category == category);
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
                Tag = infoPoint,
                Visible = ViewModel.GetInfoCategoryVisible(false, infoPoint.Category),
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