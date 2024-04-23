using System.Text.Json.Serialization;

namespace cycloid.Serizalization;

[JsonSerializable(typeof(Track))]
[JsonSerializable(typeof(Selection))]
[JsonSourceGenerationOptions(
    WriteIndented = true, 
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
public partial class TrackContext : JsonSerializerContext
{
}
