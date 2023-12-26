using System.Collections.Generic;

namespace cycloid.Routing;

public readonly record struct RouteResult(IEnumerable<RoutePoint> Points, int PointCount, long Length, long Duration)
{
    public bool IsValid => Points is not null;
}
