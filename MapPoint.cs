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

    public static explicit operator MapPoint(BasicGeoposition position) => new((float)position.Latitude, (float)position.Longitude);
    public static implicit operator BasicGeoposition(MapPoint point) => new() { Latitude = point.Latitude, Longitude = point.Longitude };
    public static implicit operator MapPoint(TrackPoint point) => new(point.Latitude, point.Longitude);

    public static MapPoint operator +(MapPoint left, MapPoint right) => new(left.Latitude + right.Latitude, left.Longitude + right.Longitude);
}
