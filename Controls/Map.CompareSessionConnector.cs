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

    private void Differences_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                {
                    TrackDifference difference = (TrackDifference)e.NewItems[0];
                    _differenceLayer.MapElements.Add(new MapPolyline
                    {
                        MapStyleSheetEntry = "Routing.Line",
                        MapStyleSheetEntryState = "Routing.diff",
                        Tag = difference,
                        Path = new Geopath(difference.OriginalPoints.Select(p => new BasicGeoposition { Longitude = p.Longitude, Latitude = p.Latitude })),
                    });
                }
                break;
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Remove:
                {
                    TrackDifference difference = (TrackDifference)e.OldItems[0];
                    int index = GetDifferenceLineIndex(difference);
                    if (index >= 0)
                    {
                        _differenceLayer.MapElements.RemoveAt(index);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Replace:
                break;
            case NotifyCollectionChangedAction.Reset:
                _differenceLayer.MapElements.Clear();
                break;
        }
    }

    private MapPolyline GetDifferenceLine(TrackDifference difference) => _differenceLayer.MapElements.OfType<MapPolyline>().FirstOrDefault(line => (TrackDifference)line.Tag == difference);

    private int GetDifferenceLineIndex(TrackDifference difference)
    {
        (MapElement line, int index) result = _differenceLayer.MapElements.Select((line, index) => (line, index)).FirstOrDefault(tuple => (TrackDifference)tuple.line.Tag == difference);
        return result.line is null ? -1 : result.index;
    }
}