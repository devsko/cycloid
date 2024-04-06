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
        foreach (string amenity in Enum.GetNames(typeof(OverpassAmenities)))
        {
            AddFilter($"amenity={amenity}");
        }
        foreach (string shop in Enum.GetNames(typeof(OverpassShops)))
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
                using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                OverpassResponse overpass = await JsonSerializer.DeserializeAsync(stream, OsmContext.Default.OverpassResponse, cancellationToken).ConfigureAwait(false);

                return overpass.elements;
            }
            else if (++retryCount > 3 || response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
            {
                return null;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }
}

#pragma warning disable IDE1006 // Naming Styles

public record struct OverpassResponse(OverpassPoint[] elements);
public record struct OverpassPoint(float lat, float lon, OverpassTags tags, OverpassBounds? bounds);
public record struct OverpassTags(OverpassAmenities? amenity, OverpassShops? shop, string name, string ele, OverpassBool? mountain_pass);
public record struct OverpassBounds(float minlat, float maxlat, float minlon, float maxlon);

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

public enum OverpassShops
{
    bakery,
    pastry,
    food,
    greengrocer,
    health_food,
    supermarket,
}

public enum OverpassBool
{
    yes,
    no,
}

#pragma warning restore IDE1006 // Naming Styles

[JsonSerializable(typeof(OverpassResponse))]
[JsonSourceGenerationOptions(
    Converters = [typeof(OverpassEnumConverter<OverpassAmenities>), typeof(OverpassEnumConverter<OverpassShops>)],
    UseStringEnumConverter = true, 
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
public partial class OsmContext : JsonSerializerContext
{ }
