using System.Text.Json;
using Microsoft.VisualStudio.Threading;

namespace cycloid.Serialization;

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

        track.PointsOfInterest.AddRange(Convert(trackFile.PointsOfInterest, Convert));

        track.CompareSession = Convert(trackFile.CompareSession, track);
        await track.InitializeCompareSessionAsync();
    }

    public static async Task SerializeAsync(Stream stream, cycloid.Track track, CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(
            stream, 
            await ConvertAsync(track, cancellationToken).ConfigureAwait(false), 
            TrackContext.Default.Track, 
            cancellationToken
        ).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
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
            CompareSession = Convert(track.CompareSession),
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

        byte[] binary = new byte[points.Length * 17];
        BinaryWriter writer = new(new MemoryStream(binary));
        foreach (TrackPoint point in points)
        {
            writer.Write(point.Latitude);
            writer.Write(point.Longitude);
            writer.Write((int)(point.Altitude * 10));
            writer.Write((int)point.Time.TotalMilliseconds);
            writer.Write((byte)point.Surface);
        }

        return binary;
    }

    private static Routing.RoutePoint[] Convert(byte[] binary)
    {
        if (binary is null)
        {
            return null;
        }

        Routing.RoutePoint[] points = new Routing.RoutePoint[binary.Length / 17];
        BinaryReader reader = new(new MemoryStream(binary));
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Routing.RoutePoint(
                reader.ReadSingle(),
                reader.ReadSingle(),
                (float)reader.ReadInt32() / 10,
                TimeSpan.FromMilliseconds(reader.ReadInt32()),
                (Surface)reader.ReadByte());
        }

        return points;
    }

    private static CompareSession? Convert(cycloid.CompareSession compareSession)
    {
        if (compareSession is null)
        {
            return null;
        }

        TrackPoint.CommonValues originalValues = compareSession.OriginalValues;

        return new CompareSession
        {
            Profile = Convert(compareSession.OriginalProfile),
            WayPoints = Convert(compareSession.OriginalWayPoints, Convert),
            TrackPoints = Convert(compareSession.OriginalTrackPoints, Convert),
            Distance = originalValues.Distance,
            Time = originalValues.Time,
            Ascent = originalValues.Ascent,
            Descent = originalValues.Descent,
        };
    }

    private static cycloid.CompareSession Convert(CompareSession? compareSession, cycloid.Track track)
    {
        if (compareSession is null)
        {
            return null;
        }

        return new cycloid.CompareSession(
            track,
            Convert(compareSession.Value.Profile),
            new TrackPoint.CommonValues(compareSession.Value.Distance, compareSession.Value.Time, compareSession.Value.Ascent, compareSession.Value.Descent),
            Convert(compareSession.Value.WayPoints, Convert),
            Convert(compareSession.Value.TrackPoints, Convert)
                .Select(segmentRoutePoints => TrackPointConverter.Convert(segmentRoutePoints, segmentRoutePoints.Length, null).Points)
                .ToArray());
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