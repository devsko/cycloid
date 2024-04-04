using Windows.Devices.Geolocation;

namespace cycloid;

public interface IMapPoint
{
    float Latitude { get; }
    float Longitude { get; }
}

public readonly record struct MapPoint(float Latitude, float Longitude) : IMapPoint
{
    public static readonly MapPoint Invalid = new(float.NaN, float.NaN);

    public bool IsValid => !float.IsNaN(Latitude);

    public static explicit operator MapPoint(BasicGeoposition position) => new() { Latitude = (float)position.Latitude, Longitude = (float)position.Longitude };
    public static implicit operator BasicGeoposition(MapPoint point) => new() { Latitude = point.Latitude, Longitude = point.Longitude };
    public static implicit operator MapPoint(TrackPoint point) => new() { Latitude = point.Latitude, Longitude = point.Longitude };

    public static MapPoint operator +(MapPoint left, MapPoint right) => new MapPoint(left.Latitude + right.Latitude, left.Longitude + right.Longitude);
}
