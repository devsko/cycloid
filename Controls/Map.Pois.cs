using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Controls.Maps;

namespace cycloid.Controls;

partial class Map
{
    private MapIcon GetOnTrackIcon(OnTrack onTrack) => _poisLayer.MapElements.OfType<MapIcon>().FirstOrDefault(element => (OnTrack)element.Tag == onTrack);

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
}