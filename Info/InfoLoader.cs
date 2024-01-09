using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.Devices.Geolocation;

namespace cycloid.Info;

public class InfoLoader
{
    private const int _minLatitude = 36;
    private const int _maxLatitude = 55;
    private const int _minLongitude = -9;
    private const int _maxLongitude = 17;

    private readonly BitArray _requested = new((_maxLatitude - _minLatitude) * (_maxLongitude - _minLongitude));
    private readonly OsmClient _client = new();

#pragma warning disable CS8425 // Async-iterator member has one or more parameters of type 'CancellationToken' but none of them is decorated with the 'EnumeratorCancellation' attribute, so the cancellation token parameter from the generated 'IAsyncEnumerable<>.GetAsyncEnumerator' will be unconsumed
    public async IAsyncEnumerable<Geopoint[]> GetAdditionalWaterPointsAsync(Geopath region, CancellationToken cancellationToken)
#pragma warning restore CS8425 // Async-iterator member has one or more parameters of type 'CancellationToken' but none of them is decorated with the 'EnumeratorCancellation' attribute, so the cancellation token parameter from the generated 'IAsyncEnumerable<>.GetAsyncEnumerator' will be unconsumed
    {
        int minLatitude = Math.Max(region.Positions.Min(p => (int)Math.Floor(p.Latitude)), _minLatitude);
        int maxLatitude = Math.Min(region.Positions.Max(p => (int)Math.Ceiling(p.Latitude)), _maxLatitude) - 1;
        int minLongitude = Math.Max(region.Positions.Min(p => (int)Math.Floor(p.Longitude)), _minLongitude);
        int maxLongitude = Math.Min(region.Positions.Max(p => (int)Math.Ceiling(p.Longitude)), _maxLongitude) - 1;

        for (int latitude = minLatitude; latitude <= maxLatitude; latitude++)
        {
            for (int longitude = minLongitude; longitude <= maxLongitude; longitude++)
            {
                int index = (latitude - _minLatitude) * (_maxLongitude - _minLongitude - 1) + (longitude - _minLongitude);

                if (!_requested[index])
                {
                    Geopoint[] points = (await _client.GetPointsAsync(latitude, longitude, "drinking_water", cancellationToken))
                        .Select(p => new Geopoint(new BasicGeoposition { Latitude = p.Lat, Longitude = p.Lon }))
                        .ToArray();
                    _requested[index] = true;

                    yield return points;
                }
            }
        }
    }
}