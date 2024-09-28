using System;
using Windows.Devices.Geolocation;

namespace cycloid;

public static class PointExtensions
{
    public static BasicGeoposition ToBasicGeoposition(this MapPoint point) => new() { Latitude = point.Latitude, Longitude = point.Longitude };
    public static MapPoint ToMapPoint(this BasicGeoposition position) => new((float)position.Latitude, (float)position.Longitude);
    public static BasicGeoposition ToBasicGeoposition(this TrackPoint point) => point.IsValid
        ? new() { Latitude = point.Latitude, Longitude = point.Longitude }
        : throw new ArgumentException("invalid", nameof(point));
}