using System;
using System.Text.Json.Serialization;

namespace cycloid.Serizalization;

public class TrackFile
{
    public Profile? Profile { get; set; }
    public WayPoint[] WayPoints { get; set; }
    public byte[][] TrackPoints { get; set; }
    public GpxFile[] GpxFiles { get; set; }
    public PointOfInterest[] PointsOfInterest { get; set; }
}

public class GpxFile
{
}

public struct Profile
{
    public int DownhillCost { get; set; }
    public float DownhillCuttoff { get; set; }
    public int UphillCost { get; set; }
    public float UphillCuttoff { get; set; }
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
    public Point Point { get; set; }
    public DateTime Created { get; set; }
    public byte Mask { get; set; }
    public string Type { get; set; }
    public string Name { get; set; }
}

[JsonSerializable(typeof(TrackFile))]
[JsonSourceGenerationOptions(
    WriteIndented = true, 
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
public partial class PoiContext : JsonSerializerContext
{
}
