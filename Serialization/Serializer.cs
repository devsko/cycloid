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

        track.RouteBuilder.Profile = new Routing.Profile
        {
            DownhillCost = trackFile.Profile.DownhillCost,
            DownhillCutoff = trackFile.Profile.DownhillCuttoff,
            UphillCost = trackFile.Profile.UphillCost,
            UphillCutoff = trackFile.Profile.UphillCuttoff,
            BikerPower = trackFile.Profile.BikerPower
        };

        await track.RouteBuilder.InitializeAsync(trackFile.WayPoints.Select((wayPoint, i) => (
            new cycloid.WayPoint(new MapPoint(wayPoint.Location.Lat, wayPoint.Location.Lon), wayPoint.IsDirectRoute, wayPoint.IsFileSplit),
            Deserialize(i == 0 ? null : trackFile.TrackPoints[i - 1])
        )));

        track.PointsOfInterest.AddRange(trackFile.PointsOfInterest.Select(pointOfInterest =>
        {
            cycloid.PointOfInterest poi = new()
            {
                Name = pointOfInterest.Name,
                Type = pointOfInterest.Type,
                Created = pointOfInterest.Created,
                Location = new MapPoint(pointOfInterest.Location.Lat, pointOfInterest.Location.Lon),
            };
            poi.InitOnTrackCount(pointOfInterest.OnTrackCount, pointOfInterest.Mask);

            return poi;
        }));

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
        Routing.Profile profile = track.RouteBuilder.Profile;

        TrackFile trackFile = new()
        {
            Profile = new Profile
            {
                DownhillCost = profile.DownhillCost,
                DownhillCuttoff = profile.DownhillCutoff,
                UphillCost = profile.UphillCost,
                UphillCuttoff = profile.UphillCutoff,
                BikerPower = profile.BikerPower
            },
            WayPoints = wayPoints.Select(wayPoint =>
                new WayPoint
                {
                    Location = new Point { Lat = wayPoint.Location.Latitude, Lon = wayPoint.Location.Longitude },
                    IsDirectRoute = wayPoint.IsDirectRoute,
                    IsFileSplit = wayPoint.IsFileSplit,
                })
                .ToArray(),
            TrackPoints = trackPoints
                .Select(trackPoints => Serialize(trackPoints))
                .ToArray(),
            PointsOfInterest = track.PointsOfInterest.Select(pointOfInterest => 
                new PointOfInterest
                {
                    Created = pointOfInterest.Created,
                    Name = pointOfInterest.Name,
                    Type = pointOfInterest.Type,
                    Location = new Point { Lat = pointOfInterest.Location.Latitude, Lon = pointOfInterest.Location.Longitude },
                    OnTrackCount = pointOfInterest.OnTrackCount.Value,
                    Mask = pointOfInterest.TrackMask,
                })
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