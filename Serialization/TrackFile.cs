using System;
using System.Text.Json.Serialization;

namespace cycloid.Serizalization;

public class TrackFile
{
    public Leg[] Legs { get; set; }
    public PointOfInterest[] PointsOfInterest { get; set; }
}

public class Leg
{
    public Point[] Waypoints { get; set; }
    public GpxPoint[] Points { get; set; }
}

public struct Point
{
    public float Lat { get; set; }
    public float Lon { get; set; }
}

public struct GpxPoint
{
    public float Lat { get; set; }
    public float Lon { get; set; }
    public TimeSpan Time { get; set; }
}

public struct PointOfInterest
{
    public DateTime Created { get; set; }
    public float Lat { get; set; }
    public float Lon { get; set; }
    public byte Mask { get; set; }
    public string Type { get; set; }
    public string Name { get; set; }
}

[JsonSerializable(typeof(TrackFile))]
public partial class PoiContext : JsonSerializerContext
{
}
