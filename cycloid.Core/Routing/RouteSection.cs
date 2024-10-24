namespace cycloid.Routing;

public class RouteSection(WayPoint start, WayPoint end)
{
    private CancellationTokenSource _cts = new();

    public WayPoint Start { get; } = start;

    public WayPoint End { get; } = end;

    public float DirectDistance { get; } = GeoCalculation.Distance(start.Location, end.Location);

    public bool IsDirectRoute
    {
        get => Start.IsDirectRoute;
        set => Start.IsDirectRoute = value;
    }

    public bool IsCanceled => _cts.IsCancellationRequested;

    public CancellationToken CancellationToken => _cts.Token;

    public void Cancel() => _cts.Cancel();

    public void ResetCancellation()
    {
        if (IsCanceled)
        {
            _cts = new CancellationTokenSource();
        }
    }
}