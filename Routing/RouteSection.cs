using System.Threading;

namespace cycloid.Routing;

public class RouteSection(WayPoint start, WayPoint end)
{
    public WayPoint Start { get; } = start;

    public WayPoint End { get; } = end;

    public float DirectDistance { get; } = GeoCalculation.Distance(start.Location, end.Location);

    public CancellationTokenSource Cancellation { get; set; }

    public bool IsCanceled => Cancellation is { IsCancellationRequested: true };

    public bool IsDirectRoute
    {
        get => Start.IsDirectRoute;
        set => Start.IsDirectRoute = value;
    }
}