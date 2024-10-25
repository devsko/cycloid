using System;
using Windows.Devices.Geolocation;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace cycloid;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class PointExtensions
{
    public static BasicGeoposition ToBasicGeoposition(this MapPoint point) => new() { Latitude = point.Latitude, Longitude = point.Longitude };
    public static BasicGeoposition ToBasicGeoposition(this MapPoint point, double altitude) => new() { Latitude = point.Latitude, Longitude = point.Longitude, Altitude = altitude };
    public static MapPoint ToMapPoint(this BasicGeoposition position) => new((float)position.Latitude, (float)position.Longitude);
    public static BasicGeoposition ToBasicGeoposition(this TrackPoint point) => point.IsValid
        ? new() { Latitude = point.Latitude, Longitude = point.Longitude }
        : throw new ArgumentException("invalid", nameof(point));
}