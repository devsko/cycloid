using Microsoft.VisualStudio.Threading;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace cycloid.Serizalization;

public static class Serializer
{
    public static async Task LoadAsync(Track track)
    {
        TrackFile trackFile = await DeserializeAsync();

        foreach (Leg leg in trackFile.Legs)
        {
            foreach (var point in leg.Waypoints)
            {
                track.RouteBuilder.AddLastPoint(new MapPoint(point.Lat, point.Lon));
            }
        }

        async Task<TrackFile> DeserializeAsync()
        {
            await TaskScheduler.Default;

            using Stream stream = await track.File.OpenStreamForReadAsync().ConfigureAwait(false);

            return await JsonSerializer.DeserializeAsync(stream, PoiContext.Default.TrackFile).ConfigureAwait(false);
        }
    }
}