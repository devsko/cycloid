using System;
using System.IO;
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

        foreach (WayPoint wayPoint in trackFile.WayPoints)
        {
            RoutePoint[] points = Deserialize(wayPoint.Points);
            track.RouteBuilder.InitializePoint(new cycloid.WayPoint(new MapPoint(wayPoint.Location.Lat, wayPoint.Location.Lon), wayPoint.IsDirectRoute), points);
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

        (cycloid.WayPoint, TrackPoint[])[] segments = await track.Points.GetSegmentsAsync(cancellationToken).ConfigureAwait(false);

        WayPoint[] wayPoints = new WayPoint[segments.Length];
        for (int i = 0; i < wayPoints.Length; i++)
        {
            (cycloid.WayPoint wayPoint, TrackPoint[] trackPoints) = segments[i];
            MapPoint location = wayPoint.Location;
            wayPoints[i] = new WayPoint
            {
                Location = new Point { Lat = location.Latitude, Lon = location.Longitude },
                IsDirectRoute = wayPoint.IsDirectRoute,
                Points = Serialize(trackPoints),
            };

            cancellationToken.ThrowIfCancellationRequested();
        }

        TrackFile trackFile = new() { WayPoints = wayPoints };

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