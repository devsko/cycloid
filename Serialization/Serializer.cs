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

        foreach (WayPoint wayPoint in trackFile.WayPoints)
        {
            track.RouteBuilder.AddLastPoint(new cycloid.WayPoint(new MapPoint(wayPoint.Point.Lat, wayPoint.Point.Lon), wayPoint.IsDirectRoute));
        }

        async Task<TrackFile> DeserializeAsync()
        {
            await TaskScheduler.Default;

            using Stream stream = await track.File.OpenStreamForReadAsync().ConfigureAwait(false);

            return await JsonSerializer.DeserializeAsync(stream, PoiContext.Default.TrackFile).ConfigureAwait(false);
        }
    }
}