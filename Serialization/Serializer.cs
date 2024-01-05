using cycloid.Routing;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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

        RoutePoint[] Deserialize(byte[] binary)
        {
            if (binary is null)
            {
                return null;
            }

            RoutePoint[] points = new RoutePoint[binary.Length / 16];
            MemoryStream stream = new(binary);
            BinaryReader reader = new(stream);
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new RoutePoint(reader.ReadSingle(), reader.ReadSingle(), (float)reader.ReadInt32() / 10, TimeSpan.FromMilliseconds(reader.ReadInt32()));
            }

            return points;
        }
    }

    public static async Task SaveAsync(Track track)
    {
        await TaskScheduler.Default;

        WayPoint[] wayPoints = new WayPoint[track.RouteBuilder.Points.Count];
        IEnumerator<TrackPoint[]> enumerator = track.Points.Segments.GetEnumerator();
        byte[] points = null;
        for (int i = 0; i < wayPoints.Length; i++)
        {
            cycloid.WayPoint wayPoint = track.RouteBuilder.Points[i];
            wayPoints[i] = new WayPoint
            {
                Location = new Point { Lat = wayPoint.Location.Latitude, Lon = wayPoint.Location.Longitude },
                IsDirectRoute = wayPoint.IsDirectRoute,
                Points = points,
            };
            if (enumerator.MoveNext())
            {
                points = Serialize(enumerator.Current);
            }
        }

        TrackFile trackFile = new() { WayPoints = wayPoints };

        await SerializeAsync();

        async Task SerializeAsync()
        {
            using IRandomAccessStream winRtStream = await track.File.OpenAsync(FileAccessMode.ReadWrite);
            winRtStream.Size = 0;
            using Stream stream = winRtStream.AsStreamForWrite();

            await JsonSerializer.SerializeAsync(stream, trackFile, PoiContext.Default.TrackFile).ConfigureAwait(false);
        }

        byte[] Serialize(TrackPoint[] points)
        {
            byte[] binary = new byte[points.Length * 16];
            MemoryStream stream = new(binary);
            BinaryWriter writer = new(stream);
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