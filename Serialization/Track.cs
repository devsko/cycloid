using System;
using cycloid.Info;

namespace cycloid.Serizalization;

public class Track
{
    public Profile Profile { get; set; }
    public WayPoint[] WayPoints { get; set; }
    public byte[][] TrackPoints { get; set; }
    public PointOfInterest[] PointsOfInterest { get; set; }
}

public class Selection
{
    public string SourceTrack { get; set; }
    public string StartLocation { get; set; }
    public string EndLocation { get; set; }
    public WayPoint[] WayPoints { get; set; }
    public PointOfInterest[] PointsOfInterest { get; set; }
}

public struct Profile
{
    public int DownhillCost { get; set; }
    public float DownhillCutoff { get; set; }
    public int UphillCost { get; set; }
    public float UphillCutoff { get; set; }
    public int BikerPower { get; set; }
}

public struct Point
{
    public float Lat { get; set; }
    public float Lon { get; set; }
}

public struct WayPoint
{
    public Point Location { get; set; }
    public bool IsDirectRoute { get; set; }
    public bool IsFileSplit { get; set; }
}

public struct PointOfInterest
{
    public Point Location { get; set; }
    public DateTime Created { get; set; }
    public byte Mask { get; set; }
    public int Count { get; set; }
    public InfoType Type { get; set; }
    public string Name { get; set; }
}
