using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Controls.Maps;

namespace cycloid.Controls;

partial class Map :
    IRecipient<CompareSessionChanged>
{
    private void RegisterCompareSessionMessages()
    {
        StrongReferenceMessenger.Default.Register<CompareSessionChanged>(this);
    }

    private MapPolyline GetDifferenceLine(TrackDifference difference) 
        => _differenceLayer.MapElements.OfType<MapPolyline>().FirstOrDefault(line => (TrackDifference)line.Tag == difference);

    private int GetDifferenceLineIndex(TrackDifference difference)
    {
        (int index, MapElement line) result = _differenceLayer.MapElements.Index().FirstOrDefault(tuple => (TrackDifference)tuple.Item.Tag == difference);
        return result.line is null ? -1 : result.index;
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
                        MapStyleSheetEntry = "Routing.Difference",
                        Tag = difference,
                        Path = new Geopath(new GeopositionEnumerable(difference.OriginalPoints.Select(p => new BasicGeoposition { Longitude = p.Longitude, Latitude = p.Latitude }))),
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
                GetDifferenceLine((TrackDifference)e.OldItems[0]).Tag = e.NewItems[0];
                break;
            case NotifyCollectionChangedAction.Reset:
                _differenceLayer.MapElements.Clear();
                break;
        }
    }

    void IRecipient<CompareSessionChanged>.Receive(CompareSessionChanged message)
    {
        if (message.OldValue is not null)
        {
            message.OldValue.Differences.CollectionChanged -= Differences_CollectionChanged;
            _differenceLayer.MapElements.Clear();
        }
        if (message.NewValue is not null)
        {
            message.NewValue.Differences.CollectionChanged += Differences_CollectionChanged;
        }
    }
}