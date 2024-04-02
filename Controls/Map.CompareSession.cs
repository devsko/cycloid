using System.Collections.Specialized;
using System.Linq;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Controls.Maps;

namespace cycloid.Controls;

partial class Map
{
    private MapPolyline GetDifferenceLine(TrackDifference difference) => _differenceLayer.MapElements.OfType<MapPolyline>().FirstOrDefault(line => (TrackDifference)line.Tag == difference);

    private int GetDifferenceLineIndex(TrackDifference difference)
    {
        (MapElement line, int index) result = _differenceLayer.MapElements.Select((line, index) => (line, index)).FirstOrDefault(tuple => (TrackDifference)tuple.line.Tag == difference);
        return result.line is null ? -1 : result.index;
    }

    private void ViewModel_CompareSessionChanged(Track.CompareSession oldCompareSession, Track.CompareSession newCompareSession)
    {
        _differenceLayer.MapElements.Clear();

        if (oldCompareSession is not null)
        {
            oldCompareSession.Differences.CollectionChanged -= Differences_CollectionChanged;
        }
        if (newCompareSession is not null)
        {
            newCompareSession.Differences.CollectionChanged += Differences_CollectionChanged;
        }
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
}