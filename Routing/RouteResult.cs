using System.Collections.Generic;

namespace cycloid.Routing;

public record struct RouteResult(TrackPoint[] Points, float MinAltitude, float MaxAltitude)
{
    public bool IsValid => Points is not null;
}
