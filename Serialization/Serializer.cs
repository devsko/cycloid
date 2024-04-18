using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace cycloid.Serizalization;

public static class Serializer
{
    public static async Task LoadAsync(Stream stream, cycloid.Track track, SynchronizationContext ui)
    {
        Track trackFile = await JsonSerializer.DeserializeAsync(stream, TrackContext.Default.Track).ConfigureAwait(false);

        track.RouteBuilder.Profile = Convert(trackFile.Profile);

        if (ui is not null)
        {
            await ui;
        }

        await track.RouteBuilder.InitializeAsync(trackFile.WayPoints.Select((wayPoint, i) => (
            Convert(wayPoint),
            Convert(i == 0 ? null : trackFile.TrackPoints[i - 1])
        )));

        track.PointsOfInterest.Clear();
        track.PointsOfInterest.AddRange(Convert(trackFile.PointsOfInterest, Convert));
    }

    public static async Task SerializeAsync(Stream stream, cycloid.Track track, CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(
            stream, 
            await ConvertAsync(track, cancellationToken).ConfigureAwait(false), 
            TrackContext.Default.Track, 
            cancellationToken
        ).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    public static async Task SerializeAsync(Stream stream, string sourceTrack, string startLocation, string endLocation, cycloid.WayPoint[] wayPoints, cycloid.PointOfInterest[] pointsOfInterest)
    {
        Selection selection = new() 
        { 
            SourceTrack = sourceTrack,
            StartLocation = startLocation,
            EndLocation = endLocation,
            WayPoints = Convert(wayPoints, Convert), 
            PointsOfInterest = Convert(pointsOfInterest, Convert),
        };
        await JsonSerializer.SerializeAsync(
            stream,
            selection,
            TrackContext.Default.Selection
        ).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    public static async Task<(string, string, string, cycloid.WayPoint[], cycloid.PointOfInterest[])> DeserializeSelectionAsync(Stream stream)
    {
        return Convert(await JsonSerializer.DeserializeAsync(stream, TrackContext.Default.Selection).ConfigureAwait(false));
    }

    public static async Task<cycloid.PointOfInterest[]> DeserializePointsOfInterestAsync(Stream stream)
    {
        return Convert(
            await JsonSerializer.DeserializeAsync(stream, TrackContext.Default.PointOfInterestArray).ConfigureAwait(false),
            Convert);
    }

    private static async Task<Track> ConvertAsync(cycloid.Track track, CancellationToken cancellationToken)
    {
        (cycloid.WayPoint[] wayPoints, TrackPoint[][] trackPoints) = await track.Points.GetSegmentsAsync(cancellationToken).ConfigureAwait(false);

        return new Track
        {
            Profile = Convert(track.RouteBuilder.Profile),
            WayPoints = Convert(wayPoints, Convert),
            TrackPoints = Convert(trackPoints, Convert),
            PointsOfInterest = Convert(track.PointsOfInterest, Convert),
        };
    }

    private static Profile Convert(Routing.Profile profile)
    {
        return new Profile
        {
            DownhillCost = profile.DownhillCost,
            DownhillCutoff = profile.DownhillCutoff,
            UphillCost = profile.UphillCost,
            UphillCutoff = profile.UphillCutoff,
            BikerPower = profile.BikerPower
        };
    }

    private static Routing.Profile Convert(Profile profile)
    {
        return new Routing.Profile
        {
            DownhillCost = profile.DownhillCost,
            DownhillCutoff = profile.DownhillCutoff,
            UphillCost = profile.UphillCost,
            UphillCutoff = profile.UphillCutoff,
            BikerPower = profile.BikerPower
        };
    }

    private static WayPoint Convert(cycloid.WayPoint wayPoint)
    {
        return new WayPoint
        {
            Location = new Point { Lat = wayPoint.Location.Latitude, Lon = wayPoint.Location.Longitude },
            IsDirectRoute = wayPoint.IsDirectRoute,
            IsFileSplit = wayPoint.IsFileSplit,
        };
    }

    private static cycloid.WayPoint Convert(WayPoint wayPoint)
    {
        return new cycloid.WayPoint(
            new MapPoint(wayPoint.Location.Lat, wayPoint.Location.Lon), 
            wayPoint.IsDirectRoute, 
            wayPoint.IsFileSplit);
    }

    private static PointOfInterest Convert(cycloid.PointOfInterest pointOfInterest)
    {
        return new PointOfInterest
        {
            Created = pointOfInterest.Created,
            Name = pointOfInterest.Name,
            Type = pointOfInterest.Type,
            Location = new Point { Lat = pointOfInterest.Location.Latitude, Lon = pointOfInterest.Location.Longitude },
            Count = pointOfInterest.OnTrackCount.Value - 1,
            Mask = (byte)(pointOfInterest.TrackMask - 1),
        };
    }

    private static cycloid.PointOfInterest Convert(PointOfInterest pointOfInterest)
    {
        cycloid.PointOfInterest poi = new()
        {
            Name = pointOfInterest.Name,
            Type = pointOfInterest.Type,
            Category = Info.InfoCategory.Get(pointOfInterest.Type),
            Created = pointOfInterest.Created,
            Location = new MapPoint(pointOfInterest.Location.Lat, pointOfInterest.Location.Lon),
        };
        poi.InitOnTrackCount(pointOfInterest.Count + 1, pointOfInterest.Mask + 1);

        return poi;
    }

    private static byte[] Convert(TrackPoint[] points)
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

    private static Routing.RoutePoint[] Convert(byte[] binary)
    {
        if (binary is null)
        {
            return null;
        }

        Routing.RoutePoint[] points = new Routing.RoutePoint[binary.Length / 16];
        BinaryReader reader = new(new MemoryStream(binary));
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Routing.RoutePoint(
                reader.ReadSingle(),
                reader.ReadSingle(),
                (float)reader.ReadInt32() / 10,
                TimeSpan.FromMilliseconds(reader.ReadInt32()));
        }

        return points;
    }

    private static (string, string, string, cycloid.WayPoint[], cycloid.PointOfInterest[]) Convert(Selection selection)
    {
        return (
            selection.SourceTrack,
            selection.StartLocation,
            selection.EndLocation,
            Convert(selection.WayPoints, Convert),
            Convert(selection.PointsOfInterest, Convert));
    }

    private static TTo[] Convert<TFrom, TTo>(IEnumerable<TFrom> from, Func<TFrom, TTo> converter)
    {
        return from.Select(converter).ToArray();
    }
}