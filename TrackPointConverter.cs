using System;
using System.Collections.Generic;
using cycloid.Routing;

namespace cycloid;

public static class TrackPointConverter
{
    public static (TrackPoint[] TrackPoints, float MinAltitude, float MaxAltitude) Convert(IEnumerable<RoutePoint> points, int count)
    {
        if (count < 2)
        {
            throw new ArgumentException();
        }

        TrackPoint[] trackPoints = new TrackPoint[count];
        float minAltitude = float.MaxValue;
        float maxAltitude = float.MinValue;
        int i = 0;
        foreach (var trackPoint in Convert(points))
        {
            trackPoints[i++] = trackPoint;
        }

        return (trackPoints, minAltitude, maxAltitude);

        IEnumerable<TrackPoint> Convert(IEnumerable<RoutePoint> points)
        {
            IEnumerator<RoutePoint> enumerator = points.GetEnumerator();
            enumerator.MoveNext();

            RoutePoint previous = enumerator.Current;

            double runningDistance = 0;
            float ascent = 0;
            float descent = 0;
            float ascentCumulated = 0;
            float descentCumulated = 0;
            RoutePoint current = default;
            bool more;

            do
            {
                double distance;
                double heading;
                double gradient;
                double speed;

                if (more = enumerator.MoveNext())
                {
                    current = enumerator.Current;

                    (distance, heading) = GeoCalculation.DistanceAndHeading(previous, current);

                    if (distance < 1e-2)
                    {
                        distance = heading = gradient = speed = 0;
                    }
                    else
                    {
                        float altitudeDiff = current.Altitude - previous.Altitude;

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

                minAltitude = Math.Min(minAltitude, previous.Altitude);
                maxAltitude = Math.Max(maxAltitude, previous.Altitude);

                yield return new TrackPoint(
                    previous.Latitude,
                    previous.Longitude,
                    previous.Altitude,
                    previous.Time,
                    (float)runningDistance,
                    (float)heading,
                    (float)gradient,
                    (float)speed,
                    ascent,
                    descent);

                previous = current;
                runningDistance += distance;
            }
            while (more);
        }
    }
}
