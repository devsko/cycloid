namespace cycloid;

public class WayPoint(MapPoint location, bool isDirectRoute)
{
    public MapPoint Location { get; init; } = location;
    public bool IsDirectRoute { get; set; } = isDirectRoute;
}