using GeoJSON.Text.Feature;
using GeoJSON.Text.Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace cycloid.Routing;

public partial class BrouterClient
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://bikerouter.de/brouter-engine/brouter/") };
    private readonly ConcurrentDictionary<Profile, Task<string>> _profiles = new();

    public async Task<Feature> GetRouteAsync(MapPoint from, MapPoint to, IEnumerable<NoGoArea> noGoAreas, Profile profile, Action retryCallback, CancellationToken cancellationToken)
    {
        string profileId = await GetProfileIdAsync(profile).ConfigureAwait(false);

        string query = FormattableString.Invariant($"?lonlats={from.Longitude},{from.Latitude}|{to.Longitude},{to.Latitude}&nogos={string.Join('|', noGoAreas.Select(noGo => FormattableString.Invariant($"{noGo.Center.Longitude},{noGo.Center.Latitude},{noGo.Radius}")))}&profile={profileId}&alternativeidx=0&format=geojson");

        while (true)
        {
            using HttpResponseMessage response = await _http.GetAsync(query, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                FeatureCollection result = await JsonSerializer.DeserializeAsync<FeatureCollection>(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

                return result?.Features.FirstOrDefault();
            }
            else if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return null;
            }

            retryCallback?.Invoke();

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IPosition> GetPositionAsync(MapPoint point, Profile profile, Action retryCallback, CancellationToken cancellationToken)
    {
        string profileId = await GetProfileIdAsync(profile).ConfigureAwait(false);

        string query = FormattableString.Invariant($"?lonlats={point.Longitude},{point.Latitude}|{point.Longitude},{point.Latitude}&profile={profileId}&alternativeidx=0&format=geojson");

        while (true)
        {
            using HttpResponseMessage response = await _http.GetAsync(query, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                FeatureCollection result = await JsonSerializer.DeserializeAsync<FeatureCollection>(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

                return (result?.Features.FirstOrDefault()?.Geometry as LineString)?.Coordinates[0];
            }
            else if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
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
}

public readonly record struct Profile(int DownhillCost, float DownhillCutoff, int UphillCost, float UphillCutoff, int BikerPower);
public readonly record struct NoGoArea(MapPoint Center, ulong Radius);

public class ProfileResponse
{
    [JsonPropertyName("profileid")]
    public string ProfileId { get; init; }
}