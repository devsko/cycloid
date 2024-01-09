using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace cycloid.Info;

public class OsmClient
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://overpass-api.de/api/interpreter/") };
    public async Task<OverpassPoint[]> GetPointsAsync(int bottom, int left, string amenity, CancellationToken cancellationToken)
    {
        string content = FormattableString.Invariant($"""
            [out:json][timeout:900];
            node[amenity={amenity}]({bottom},{left},{bottom + 1},{left + 1});
            out skel geom qt;
            """);

        int retryCount = 0;
        while (true)
        {
            using HttpResponseMessage response = await _http.PostAsync("", new StringContent(content), cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using Stream stream = await response.Content.ReadAsStreamAsync();

                OverpassResponse overpass = await JsonSerializer.DeserializeAsync(stream, OsmContext.Default.OverpassResponse, cancellationToken);

                return overpass.Elements;
            }
            else if (++retryCount > 3 || response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
            {
                return null;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }
}

public record struct OverpassResponse([property:JsonPropertyName("elements")] OverpassPoint[] Elements);
public record struct OverpassPoint([property: JsonPropertyName("id")] long Id, [property: JsonPropertyName("lat")] float Lat, [property: JsonPropertyName("lon")] float Lon);


[JsonSerializable(typeof(OverpassResponse))]
public partial class OsmContext : JsonSerializerContext
{ }
