using System;

namespace cycloid.Routing;

public interface IRoutePoint : IMapPoint
{
    float Altitude { get; }
    TimeSpan Time { get; }
    Surface Surface { get; }
}

public readonly record struct RoutePoint(float Latitude, float Longitude, float Altitude, TimeSpan Time, Surface Surface) : IRoutePoint
{
    public static RoutePoint FromPosition(GeoJSON.Text.Geometry.IPosition position, TimeSpan time, Surface surface) => new((float)position.Latitude, (float)position.Longitude, (float)(position.Altitude ?? 0), time, surface);
    public static RoutePoint FromMapPoint(MapPoint point, float altitude, TimeSpan time, Surface surface) => new(point.Latitude, point.Longitude, altitude, time, surface);
    public static RoutePoint FromTrackPoint(TrackPoint point) => new(point.Latitude, point.Longitude, point.Altitude, point.Time, point.Surface);
}
