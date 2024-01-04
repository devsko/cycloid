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
            track.RouteBuilder.AddLastPoint(new cycloid.WayPoint(new MapPoint(wayPoint.Location.Lat, wayPoint.Location.Lon), wayPoint.IsDirectRoute));
        }

        async Task<TrackFile> DeserializeAsync()
        {
            await TaskScheduler.Default;

            using Stream stream = await track.File.OpenStreamForReadAsync().ConfigureAwait(false);

            return await JsonSerializer.DeserializeAsync(stream, PoiContext.Default.TrackFile).ConfigureAwait(false);
        }
    }

    public static async Task SaveAsync(Track track)
    {
        await TaskScheduler.Default;

        WayPoint[] wayPoints = new WayPoint[track.RouteBuilder.Points.Count];
        IEnumerator<TrackPoint[]> enumerator = track.Points.Segments.GetEnumerator();
        for (int i = 0; i < wayPoints.Length; i++)
        {
            cycloid.WayPoint wayPoint = track.RouteBuilder.Points[i];
            byte[] points = null;
            if (enumerator.MoveNext())
            {
                points = Serialize(enumerator.Current);
            }
            wayPoints[i] = new WayPoint
            {
                Location = new Point { Lat = wayPoint.Location.Latitude, Lon = wayPoint.Location.Longitude },
                IsDirectRoute = wayPoint.IsDirectRoute,
                Points = points,
            };
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

        byte[] Serialize(TrackPoint[] segment)
        {
            byte[] binary = new byte[segment.Length * 14];
            MemoryStream stream = new(binary);
            BinaryWriter writer = new(stream);
            foreach (TrackPoint point in segment)
            {
                (float latitude, float longitude, int time, ushort altitude) = point.RawValues;
                writer.Write(latitude);
                writer.Write(longitude);
                writer.Write(time);
                writer.Write(altitude);
            }

            return binary;
        }
    }
}