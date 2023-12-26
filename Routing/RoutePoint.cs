using System;

namespace cycloid.Routing;

public interface IRoutePoint : IMapPoint
{
    float Altitude { get; }
    TimeSpan Time { get; }
}

public readonly record struct RoutePoint(float Latitude, float Longitude, float Altitude, TimeSpan Time) : IRoutePoint;
