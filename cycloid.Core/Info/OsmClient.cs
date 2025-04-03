using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace cycloid.Info;

public class OsmClient
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://overpass-api.de/api/interpreter/") };
    private readonly string _query = CreateQuery();

    private static string CreateQuery()
    {
        StringBuilder query = new();
        AddFilter("mountain_pass=yes");
        foreach (string amenity in Enum.GetNames<OverpassAmenities>())
        {
            AddFilter($"amenity={amenity}");
        }
        foreach (string shop in Enum.GetNames<OverpassShops>())
        {
            AddFilter($"shop={shop}");
        }

        return query.ToString();

        void AddFilter(string filter)
        {
            query.AppendLine($"node[{filter}];");
            query.AppendLine($"way[{filter}];");
        }
    }

    public async Task<OverpassPoint[]> GetPointsAsync(MapPoint point, MapPoint size, CancellationToken cancellationToken)
    {
        MapPoint point2 = point + size;
        string content = FormattableString.Invariant($"""
            [out:json][timeout:900][bbox:{point.Latitude},{point.Longitude},{point2.Latitude},{point2.Longitude}];
            (
            {_query}
            );
            out geom qt;
            """);

        int retryCount = 0;
        while (true)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await _http.PostAsync("", new StringContent(content), cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                OverpassResponse overpass = await response.Content.ReadFromJsonAsync(OsmContext.Default.OverpassResponse, cancellationToken).ConfigureAwait(false);

                return overpass.Elements;
            }
            catch (HttpRequestException) when (retryCount < 3 && response?.StatusCode is not (HttpStatusCode.BadGateway or HttpStatusCode.NotFound))
            {
                retryCount++;
            }
            finally
            {
                response?.Dispose();
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }
}

public record struct OverpassResponse(OverpassPoint[] Elements);
public record struct OverpassPoint(float Lat, float Lon, OverpassTags Tags, OverpassBounds? Bounds);
public record struct OverpassTags(OverpassAmenities? Amenity, OverpassShops? Shop, string Name, string Ele, OverpassBool? MountainPass);
public record struct OverpassBounds(float Minlat, float Maxlat, float Minlon, float Maxlon);

[JsonConverter(typeof(OverpassEnumConverter<OverpassAmenities>))]
public enum OverpassAmenities
{
    drinking_water,
    toilets,
    fuel,
    fast_food,
    ice_cream,
    cafe,
    bar,
    pub,
    restaurant,
}

[JsonConverter(typeof(OverpassEnumConverter<OverpassShops>))]
public enum OverpassShops
{
    bakery,
    pastry,
    food,
    greengrocer,
    health_food,
    supermarket,
}

[JsonConverter(typeof(OverpassEnumConverter<OverpassBool>))]
public enum OverpassBool
{
    yes,
    no,
}

[JsonSerializable(typeof(OverpassResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
public partial class OsmContext : JsonSerializerContext
{ 
}
