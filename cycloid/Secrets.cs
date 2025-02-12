using System.Text.Json.Serialization;

namespace cycloid;

public class Secrets
{
    public required string BingServiceApiKey { get; init; }
    public required string GoogleServiceApiKey { get; init; }
    public required string StravaClientId { get; init; }
    public required string StravaClientSecret { get; init; }

    public string this[string key] => key switch
    {
        nameof(BingServiceApiKey) => BingServiceApiKey,
        nameof(GoogleServiceApiKey) => GoogleServiceApiKey,
        nameof(StravaClientId) => StravaClientId,
        nameof(StravaClientSecret) => StravaClientSecret,
        _ => throw new KeyNotFoundException(),
    };
}

[JsonSerializable(typeof(Secrets))]
[JsonSourceGenerationOptions(ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip)]
public partial class SecretsContext : JsonSerializerContext
{
}
