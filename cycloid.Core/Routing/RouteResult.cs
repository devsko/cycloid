namespace cycloid.Routing;

public readonly record struct RouteResult(TrackPoint[] Points, float MinAltitude, float MaxAltitude)
{
    public bool IsValid => Points is not null;
}
