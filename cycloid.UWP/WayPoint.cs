namespace cycloid;

public class WayPoint(MapPoint location, bool isDirectRoute, bool isFileSplit)
{
    public MapPoint Location { get; init; } = location;
    public bool IsDirectRoute { get; set; } = isDirectRoute;
    public bool IsFileSplit { get; set; } = isFileSplit;
}