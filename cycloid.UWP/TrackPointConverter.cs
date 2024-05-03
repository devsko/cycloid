using System;
using System.Collections.Generic;
using cycloid.Routing;

namespace cycloid;

public enum Surface : byte
{
    paved,
    asphalt,
    chipseal,
    concrete,
    paving_stones,

    sett,
    unhewn_cobblestone,
    cobblestone,
    bricks,
    metal,
    wood,
    stepping_stones,
    rubber,
    UnknownLikelyPaved, // highway=motorway/motorway_link/trunk/trunk_link/primary/primary_link/secondary/secondary_link/tertiary/tertiary_link/unclassified/residential/living_street/service

    unpaved,
    compacted,
    fine_gravel,
    gravel,
    shells,
    rock,
    pebblestone,
    ground,
    dirt,
    earth,
    grass,
    grass_paver,
    metal_grid,
    mud,
    sand,
    woodchips,
    snow,
    ice,
    salt,
    UnknownLikelyUnpaved, // highway=track/bridleway/footway/path
    
    Unknown,
}

public enum Highway : byte
{
    motorway,
    motorway_link,
    trunk,
    trunk_link,
    primary,
    primary_link,
    secondary,
    secondary_link,
    tertiary,
    tertiary_link,
    unclassified,
    residential,
    living_street,
    service,

    track,
    bridleway,
    footway,
    path,
    
    Unknown,
}

public readonly record struct SurfacePart(float Distance, Surface Surface);

public static class TrackPointConverter
{
    public static RouteResult Convert(RoutePoint start, RoutePoint end)
    {
        return Convert([start, end], 2, null);
    }

    public static RouteResult Convert(IEnumerable<RoutePoint> points, int count, IEnumerable<SurfacePart> surfaces)
    {
        if (count < 2)
        {
            throw new ArgumentException();
        }

        TrackPoint[] trackPoints = new TrackPoint[count];
        float minAltitude = float.MaxValue;
        float maxAltitude = float.MinValue;
        int i = 0;
        foreach (TrackPoint trackPoint in Convert(points, surfaces))
        {
            trackPoints[i++] = trackPoint;
        }

        return new RouteResult(trackPoints, minAltitude, maxAltitude);

        IEnumerable<TrackPoint> Convert(IEnumerable<RoutePoint> points, IEnumerable<SurfacePart> surfaces)
        {
            IEnumerator<SurfacePart> surfaceEnumerator = surfaces?.GetEnumerator();
            surfaceEnumerator?.MoveNext();
            SurfacePart currentSurface = surfaces is null ? default : surfaceEnumerator.Current;
            float surfaceDistance = surfaces is null ? float.PositiveInfinity : currentSurface.Distance;

            IEnumerator<RoutePoint> enumerator = points.GetEnumerator();
            enumerator.MoveNext();

            RoutePoint previous = enumerator.Current;
            float previousAltitude = previous.Altitude ?? 0; // TODO null Altitude at beginning?

            double runningDistance = 0;
            float ascent = 0;
            float descent = 0;
            float ascentCumulated = 0;
            float descentCumulated = 0;
            RoutePoint current = default;
            float currentAltitude = 0;
            bool more;

            do
            {
                double distance;
                double heading;
                double gradient;
                double speed;

                // TODO calculated runningDistance doesn't match exactly the rounded distances from Brouter
                // use the last part for now
                while (Math.Floor(runningDistance) >= surfaceDistance )
                {
                    if (!surfaceEnumerator.MoveNext())
                    {
                        break;
                    }
                    currentSurface = surfaceEnumerator.Current;
                    surfaceDistance += currentSurface.Distance;
                }

                if (more = enumerator.MoveNext())
                {
                    current = enumerator.Current;
                    currentAltitude = current.Altitude ?? previousAltitude;

                    (distance, heading) = GeoCalculation.DistanceAndHeading(previous, current);

                    if (distance < 1e-2)
                    {
                        distance = heading = gradient = speed = 0;
                    }
                    else
                    {
                        float altitudeDiff = currentAltitude - previousAltitude;

                        ascentCumulated += altitudeDiff;
                        descentCumulated += altitudeDiff;

                        gradient = 0;
                        if (ascentCumulated > 0)
                        {
                            gradient = ascentCumulated * 100 / distance;
                            ascent += ascentCumulated;
                            ascentCumulated = 0;
                        }
                        else if (ascentCumulated < -10)
                        {
                            ascentCumulated = -10;
                        }

                        if (descentCumulated < 0)
                        {
                            gradient = descentCumulated * 100 / distance;
                            descent -= descentCumulated;
                            descentCumulated = 0;
                        }
                        else if (descentCumulated > 10)
                        {
                            descentCumulated = 10;
                        }

                        speed = distance / 1_000 / (current.Time - previous.Time).TotalHours;
                    }
                }
                else
                {
                    distance = heading = gradient = speed = 0;
                }

                minAltitude = Math.Min(minAltitude, previousAltitude);
                maxAltitude = Math.Max(maxAltitude, previousAltitude);

                yield return new TrackPoint(
                    previous.Latitude,
                    previous.Longitude,
                    previousAltitude,
                    previous.Time,
                    (float)runningDistance,
                    (float)heading,
                    (float)gradient,
                    (float)speed,
                    ascent,
                    descent,
                    surfaceEnumerator?.Current.Surface ?? previous.Surface);

                previous = current;
                previousAltitude = currentAltitude;
                runningDistance += distance;
            }
            while (more);
        }
    }
}
