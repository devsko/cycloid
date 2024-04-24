using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using GeoJSON.Text.Feature;
using GeoJSON.Text.Geometry;

namespace cycloid.Routing;

public partial class BrouterClient
{
    private static readonly Dictionary<string, Surface> s_surfaces = CreateKnownValues<Surface>();
    private static readonly Dictionary<string, Highway> s_highways = CreateKnownValues<Highway>();

    private static Dictionary<string, T> CreateKnownValues<T>()
    {
        Dictionary<string, T> knownValues = [];

        string[] names = Enum.GetNames(typeof(T));
        Array values = Enum.GetValues(typeof(T));

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (char.IsLower(name[0]))
            {
                knownValues.Add(name, (T)values.GetValue(i));
            }
        }

        return knownValues;
    }

    private readonly HttpClient _http = new() 
    { 
        BaseAddress = new Uri("https://bikerouter.de/brouter-engine/brouter/"), 
        Timeout = Timeout.InfiniteTimeSpan 
    };
    private readonly ConcurrentDictionary<Profile, Task<string>> _profiles = new();

    public async Task<(IEnumerable<RoutePoint>, int, IEnumerable<SurfacePart>)> GetRouteAsync(MapPoint from, MapPoint to, IEnumerable<NoGoArea> noGoAreas, Profile profile, Action retryCallback, CancellationToken cancellationToken)
    {
        string profileId = await GetProfileIdAsync(profile).ConfigureAwait(false);

        string query = FormattableString.Invariant($"?lonlats={from.Longitude},{from.Latitude}|{to.Longitude},{to.Latitude}&nogos={string.Join('|', noGoAreas.Select(noGo => FormattableString.Invariant($"{noGo.Center.Longitude},{noGo.Center.Latitude},{noGo.Radius}")))}&profile={profileId}&alternativeidx=0&format=geojson");

        int retryCount = 0;
        while (true)
        {
            using HttpResponseMessage response = await _http.GetAsync(query, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                FeatureCollection result = await JsonSerializer.DeserializeAsync<FeatureCollection>(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

                Feature feature = result?.Features.FirstOrDefault();
                if (feature is null)
                {
                    return default;
                }

                //long GetProperty(string name) => long.Parse(((JsonElement)feature.Properties[name]).GetString()!);

                //long length = GetProperty("track-length");
                //long duration = GetProperty("total-time");
                //long ascend = GetProperty("filtered ascend");

                ReadOnlyCollection<IPosition> positions = ((LineString)feature.Geometry).Coordinates;
                IEnumerable<float> times = ((JsonElement)feature.Properties["times"]).EnumerateArray().Select(e => e.GetSingle());
                IEnumerable<SurfacePart> surfaces = ((JsonElement)feature.Properties["messages"]).EnumerateArray().Skip(1).Select(CreateSurfacePart);

                IEnumerable<RoutePoint> points = positions.Zip(times,
                    (position, time) => new RoutePoint(
                        (float)position.Latitude,
                        (float)position.Longitude,
                        (float)(position.Altitude ?? 0),
                        TimeSpan.FromSeconds(time),
                        Surface.Unknown));

                return (points, positions.Count, surfaces);
            }
            else if (++retryCount > 3 || !await CanRetryAsync(response).ConfigureAwait(false))
            {
                return default;
            }

            retryCallback?.Invoke();

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        static SurfacePart CreateSurfacePart(JsonElement element)
        {
            IEnumerator<JsonElement> enumerator = element.EnumerateArray().GetEnumerator();
            enumerator.MoveNext();
            // Longitude
            enumerator.MoveNext();
            // Latitude
            enumerator.MoveNext();
            // Elevation
            enumerator.MoveNext();
            // Distance
            int distance = int.Parse(enumerator.Current.GetString());
            enumerator.MoveNext();
            // CostPerKm
            enumerator.MoveNext();
            // ElevCost
            enumerator.MoveNext();
            // TurnCost
            enumerator.MoveNext();
            // NodeCost
            enumerator.MoveNext();
            // InitialCost
            enumerator.MoveNext();
            // WayTags
            string tags = enumerator.Current.GetString();

            if (!s_surfaces.TryGetValue(GetValue(tags, "surface"), out Surface surface))
            {
                surface = !s_highways.TryGetValue(GetValue(tags, "highway"), out Highway highway)
                    ? Surface.Unknown
                    : highway <= Highway.service
                        ? Surface.UnknownLikelyPaved
                        : Surface.UnknownLikelyUnpaved;
            }

            // NodeTags
            // Time
            // Energy

            return new SurfacePart(distance, surface);

            static string GetValue(string tags, string tag)
            {
                int index = tags.IndexOf($"{tag}=");
                if (index == -1)
                {
                    return "";
                }

                index += tag.Length + 1;
                int endIndex = tags.IndexOf(' ', index);

                return endIndex == -1 ? tags[index..] : tags[index..endIndex];
            }
        }
    }

    public async Task<RoutePoint?> GetPositionAsync(MapPoint point, Profile profile, Action retryCallback, CancellationToken cancellationToken)
    {
        string profileId = await GetProfileIdAsync(profile).ConfigureAwait(false);

        string query = FormattableString.Invariant($"?lonlats={point.Longitude},{point.Latitude}|{point.Longitude},{point.Latitude}&profile={profileId}&alternativeidx=0&format=geojson");

        int retryCount = 0;
        while (true)
        {
            using HttpResponseMessage response = await _http.GetAsync(query, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                FeatureCollection result = await JsonSerializer.DeserializeAsync<FeatureCollection>(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

                Feature feature = result?.Features.FirstOrDefault();
                if (feature is not null)
                {
                    IPosition position = (feature.Geometry as LineString)?.Coordinates[0];
                    if (position is not null)
                    {
                        return RoutePoint.FromPosition(position, TimeSpan.Zero, Surface.Unknown);
                    }
                }

                return null;
            }
            else if (++retryCount > 3 || !await CanRetryAsync(response).ConfigureAwait(false))
            {
                return null;
            }

            retryCallback?.Invoke();

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<string> GetProfileIdAsync(Profile profile)
    {
        return _profiles.GetOrAdd(profile, CreateProfileAsync);

        async Task<string> CreateProfileAsync(Profile profile)
        {
            string profileValue = _profileTemplate
                .Replace("{downhillcost}", profile.DownhillCost.ToString(CultureInfo.InvariantCulture))
                .Replace("{downhillcutoff}", profile.DownhillCutoff.ToString(CultureInfo.InvariantCulture))
                .Replace("{uphillcost}", profile.UphillCost.ToString(CultureInfo.InvariantCulture))
                .Replace("{uphillcutoff}", profile.UphillCutoff.ToString(CultureInfo.InvariantCulture))
                .Replace("{bikerPower}", profile.BikerPower.ToString(CultureInfo.InvariantCulture));

            using HttpResponseMessage response = await _http.PostAsync("profile", new StringContent(profileValue), CancellationToken.None).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            ProfileResponse result = await JsonSerializer.DeserializeAsync<ProfileResponse>(contentStream, cancellationToken: CancellationToken.None).ConfigureAwait(false) ?? throw new Exception();

            return result.ProfileId;
        }
    }

    private async Task<bool> CanRetryAsync(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return false;
        }

        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        return content.StartsWith("operation killed by thread-priority-watchdog");
    }
}

public readonly record struct Profile(int DownhillCost, float DownhillCutoff, int UphillCost, float UphillCutoff, int BikerPower)
{
    public const int DefaultDownhillCost = 80;
    public const float DefaultDownhillCutoff = .5f;
    public const int DefaultUphillCost = 100;
    public const float DefaultUphillCutoff = 3.6f;
    public const int DefaultBikerPower = 170;

    public Profile() : this(DefaultDownhillCost, DefaultDownhillCutoff, DefaultUphillCost, DefaultUphillCutoff, DefaultBikerPower)
    { }
}

public readonly record struct NoGoArea(MapPoint Center, ulong Radius);

public class ProfileResponse
{
    [JsonPropertyName("profileid")]
    public string ProfileId { get; init; }
}