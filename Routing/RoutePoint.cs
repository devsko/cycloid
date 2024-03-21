using System;

namespace cycloid.Routing;

public interface IRoutePoint : IMapPoint
{
    float Altitude { get; }
    TimeSpan Time { get; }
}

public readonly record struct RoutePoint(float Latitude, float Longitude, float Altitude, TimeSpan Time) : IRoutePoint
{
    public static RoutePoint FromPosition(GeoJSON.Text.Geometry.IPosition position, TimeSpan time) => new((float)position.Latitude, (float)position.Longitude, (float)(position.Altitude ?? 0), time);
    public static RoutePoint FromMapPoint(MapPoint point, float altitude, TimeSpan time) => new(point.Latitude, point.Longitude, altitude, time);
}
