using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace cycloid.Info;

public class OsmClient
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://overpass-api.de/api/interpreter/") };
    private readonly string _query = CreateQuery();

    private static string CreateQuery()
    {
        StringBuilder query = new();
        AddFilter("mountain_pass=yes");
        foreach (string amenity in
#if NETSTANDARD
            Enum.GetNames(typeof(OverpassAmenities)))
#else
            Enum.GetNames<OverpassAmenities>())
#endif
        {
            AddFilter($"amenity={amenity}");
        }
        foreach (string shop in
#if NETSTANDARD
            Enum.GetNames(typeof(OverpassShops)))
#else
            Enum.GetNames<OverpassShops>())
#endif
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
            using HttpResponseMessage response = await _http.PostAsync("", new StringContent(content), cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using Stream stream = await response.Content.
#if NETSTANDARD
                    ReadAsStreamAsync()
#else
                    ReadAsStreamAsync(cancellationToken)
#endif
                    .ConfigureAwait(false);

                OverpassResponse overpass = await JsonSerializer.DeserializeAsync(stream, OsmContext.Default.OverpassResponse, cancellationToken).ConfigureAwait(false);

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
{ }
