using System.Text.Json.Serialization;

namespace cycloid.Wahoo;

public record class Track(PointOfInterest[] PointsOfInterest);
public record class Location(float Lat, float Lon);
public record class PointOfInterest(string Name, Location Location, string Type);

[JsonSerializable(typeof(Track))]
[JsonSourceGenerationOptions()]
public partial class TrackContext : JsonSerializerContext
{ }
