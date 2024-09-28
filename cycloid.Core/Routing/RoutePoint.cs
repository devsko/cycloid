using System;

namespace cycloid.Routing;

public readonly record struct RoutePoint(float Latitude, float Longitude, float? Altitude, TimeSpan Time, Surface Surface) : IMapPoint
{
    public static RoutePoint FromPosition(GeoJSON.Text.Geometry.IPosition position, TimeSpan time, Surface surface) => new((float)position.Latitude, (float)position.Longitude, (float?)position.Altitude, time, surface);
    public static RoutePoint FromMapPoint(MapPoint point, float altitude, TimeSpan time, Surface surface) => new(point.Latitude, point.Longitude, altitude, time, surface);
    public static RoutePoint FromTrackPoint(TrackPoint point) => new(point.Latitude, point.Longitude, point.Altitude, point.Time, point.Surface);
}
