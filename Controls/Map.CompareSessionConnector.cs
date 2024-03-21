using System.Collections.Specialized;
using System.Linq;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Controls.Maps;

namespace cycloid.Controls;

partial class Map
{
    private void Connect(Track.CompareSession compareSession)
    {
        compareSession.Differences.CollectionChanged += Differences_CollectionChanged;
    }

    private void Disconnect(Track.CompareSession compareSession)
    {
        compareSession.Differences.CollectionChanged -= Differences_CollectionChanged;
    }

    private MapPolyline GetDifferenceLine(TrackDifference difference) => _differenceLayer.MapElements.OfType<MapPolyline>().First(line => (TrackDifference)line.Tag == difference);

    private void Differences_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                TrackDifference difference = (TrackDifference)e.NewItems[0];
                _differenceLayer.MapElements.Add(new MapPolyline
                {
                    MapStyleSheetEntry = "Routing.Line",
                    MapStyleSheetEntryState = "Routing.diff",
                    Tag = difference,
                    Path = new Geopath(difference.OriginalPoints.Select(p => new BasicGeoposition { Longitude = p.Longitude, Latitude = p.Latitude })),
                });
                break;
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Remove:
                break;
            case NotifyCollectionChangedAction.Replace:
                break;
            case NotifyCollectionChangedAction.Reset:
                break;
        }
    }
}