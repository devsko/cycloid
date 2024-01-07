using System;
using System.Collections;
using System.Collections.Generic;
using cycloid.Routing;

namespace cycloid;

public struct TrackAggregator
{
    private TrackPoint[] _points = [];

    public TrackAggregator()
    { }

    public readonly TrackPoint[] ToArray() => _points;

    public void Add(IEnumerable<RoutePoint> points, int count)
    {
        if (count >= 2)
        {
            _points = [.. _points, .. new TrackPointCollection(Convert(), count)];
        }

        IEnumerable<TrackPoint> Convert()
        {
            IEnumerator<RoutePoint> enumerator = points.GetEnumerator();
            enumerator.MoveNext();

            RoutePoint previous = enumerator.Current;

            double runningDistance = 0;
            double distance = 0;
            double heading = 0;
            double gradient = 0;
            double speed = 0;
            float ascent = 0;
            float descent = 0;
            float ascentCumulated = 0;
            float descentCumulated = 0;
            RoutePoint current = default;
            bool more;

            do
            {
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
                            gradient = -descentCumulated * 100 / distance;
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
                    runningDistance = distance = heading = gradient = speed = ascent = descent = 0;
                }

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

    private class TrackPointCollection(IEnumerable<TrackPoint> points, int count) : ICollection<TrackPoint>
    {
        private readonly IEnumerable<TrackPoint> _points = points;
        private readonly int _count = count;

        public int Count => _count;

        public IEnumerator<TrackPoint> GetEnumerator() => _points.GetEnumerator();

        public bool IsReadOnly => throw new NotImplementedException();

        public void Add(TrackPoint item) => throw new NotImplementedException();

        public void Clear() => throw new NotImplementedException();

        public bool Contains(TrackPoint item) => throw new NotImplementedException();

        public void CopyTo(TrackPoint[] array, int arrayIndex) => throw new NotImplementedException();

        public bool Remove(TrackPoint item) => throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
    }
}
