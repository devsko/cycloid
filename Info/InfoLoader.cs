// Types of POI https://wiki.openstreetmap.org/wiki/Key:amenity

// drinking_water
// toilets
// shower
// (shelter)

// (atm|bank)
// fuel
// fast_food
// cafe
// pub
// restaurant
// nightclub

// https://wiki.openstreetmap.org/wiki/Key:shop

// bakery|pastry
// food|greengrocer|health_food
// supermarket|(convenience)|(dairy)|(farm)
// ice_cream
// bicycle


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

    public async IAsyncEnumerable<IEnumerable<InfoPoint>> GetAdditionalInfoPointsAsync(Geopath region, [EnumeratorCancellation] CancellationToken cancellationToken)
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
                    IEnumerable<InfoPoint> points = (await _client.GetPointsAsync(latitude, longitude, cancellationToken))
                        .Select(InfoPoint.FromOverpassPoint);

                    cancellationToken.ThrowIfCancellationRequested();

                    _requested[index] = true;

                    yield return points;
                }
            }
        }
    }
}