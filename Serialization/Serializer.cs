using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using cycloid.Routing;
using Microsoft.VisualStudio.Threading;
using Windows.Storage;
using Windows.Storage.Streams;

namespace cycloid.Serizalization;

public static class Serializer
{
    public static async Task LoadAsync(Track track)
    {
        TrackFile trackFile = await DeserializeAsync();

        for (int i = 0; i < trackFile.WayPoints.Length; i++)
        {
            WayPoint wayPoint = trackFile.WayPoints[i];
            RoutePoint[] routePoints = Deserialize(i > 0 ? trackFile.TrackPoints[i - 1] : null);

            track.RouteBuilder.InitializePoint(
                new cycloid.WayPoint(new MapPoint(wayPoint.Location.Lat, wayPoint.Location.Lon), wayPoint.IsDirectRoute), 
                routePoints);
        }

        async Task<TrackFile> DeserializeAsync()
        {
            await TaskScheduler.Default;

            using Stream stream = await track.File.OpenStreamForReadAsync().ConfigureAwait(false);

            return await JsonSerializer.DeserializeAsync(stream, PoiContext.Default.TrackFile).ConfigureAwait(false);
        }

        static RoutePoint[] Deserialize(byte[] binary)
        {
            if (binary is null)
            {
                return null;
            }

            RoutePoint[] points = new RoutePoint[binary.Length / 16];
            BinaryReader reader = new(new MemoryStream(binary));
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new RoutePoint(
                    reader.ReadSingle(), 
                    reader.ReadSingle(), 
                    (float)reader.ReadInt32() / 10, 
                    TimeSpan.FromMilliseconds(reader.ReadInt32()));
            }

            return points;
        }
    }

    public static async Task SaveAsync(Track track, CancellationToken cancellationToken)
    {
        await TaskScheduler.Default;

        (cycloid.WayPoint[] wayPoints, TrackPoint[][] trackPoints) = await track.Points.GetSegmentsAsync(cancellationToken).ConfigureAwait(false);

        TrackFile trackFile = new()
        {
            WayPoints = wayPoints.Select(wayPoint => 
                new WayPoint
                {
                    Location = new Point { Lat = wayPoint.Location.Latitude, Lon = wayPoint.Location.Longitude },
                    IsDirectRoute = wayPoint.IsDirectRoute,
                })
                .ToArray(),
            TrackPoints = trackPoints
                .Select(trackPoints => Serialize(trackPoints))
                .ToArray()
        };

        using IRandomAccessStream winRtStream = await track.File.OpenAsync(FileAccessMode.ReadWrite).AsTask().ConfigureAwait(false);
        winRtStream.Size = 0;
        using Stream stream = winRtStream.AsStreamForWrite();

        await JsonSerializer.SerializeAsync(stream, trackFile, PoiContext.Default.TrackFile, cancellationToken).ConfigureAwait(false);

        static byte[] Serialize(TrackPoint[] points)
        {
            if (points is null)
            {
                return null;
            }

            byte[] binary = new byte[points.Length * 16];
            BinaryWriter writer = new(new MemoryStream(binary));
            foreach (TrackPoint point in points)
            {
                writer.Write(point.Latitude);
                writer.Write(point.Longitude);
                writer.Write((int)(point.Altitude * 10));
                writer.Write((int)point.Time.TotalMilliseconds);
            }

            return binary;
        }
    }
}