using System;
using System.Text.Json.Serialization;

namespace cycloid.Serizalization;

public class TrackFile
{
    public WayPoint[] WayPoints { get; set; }
    public GpxFile[] GpxFiles { get; set; }
    public PointOfInterest[] PointsOfInterest { get; set; }
}

public class GpxFile
{
}

public struct Point
{
    public float Lat { get; set; }
    public float Lon { get; set; }
}

public struct WayPoint
{
    public Point Location { get; set; }
    public byte[] Points { get; set; }
    public bool IsDirectRoute { get; set; }
    public bool IsFileSplit { get; set; }
}

public struct PointOfInterest
{
    public Point Point { get; set; }
    public DateTime Created { get; set; }
    public byte Mask { get; set; }
    public string Type { get; set; }
    public string Name { get; set; }
}

[JsonSerializable(typeof(TrackFile))]
public partial class PoiContext : JsonSerializerContext
{
}
