using System.Threading;

namespace cycloid.Routing;

public class RouteSection(MapPoint start, MapPoint end)
{
    public MapPoint Start { get; } = start;
    public MapPoint End { get; } = end;
    public float DirectDistance { get; } = GeoCalculation.Distance(start, end);
    public CancellationTokenSource Cancellation { get; } = new();

    public bool IsCanceled => Cancellation.IsCancellationRequested;
}