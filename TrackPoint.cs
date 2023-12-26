using Windows.Devices.Geolocation;

namespace cycloid;

public readonly record struct TrackPoint(float Latitude, float Longitude, float Altitude, float Gradient, float Heading)
{
    public static implicit operator BasicGeoposition(TrackPoint point) => new()
    {
        Latitude = point.Latitude,
        Longitude = point.Longitude,
    };
}
