using System;
using System.Collections.Generic;
using System.Linq;
using cycloid.Routing;
using Seg = (cycloid.Routing.RouteSection Section, cycloid.MapPoint[] Points, System.Collections.Generic.Dictionary<cycloid.MapPoint, int> Indices);

namespace cycloid;

partial class Track
{
    public class CompareSession
    {
        private readonly Seg[] _segments;

        public CompareSession(RouteBuilder routeBuilder, MapPoint[][] sectionPoints)
        {
            _segments = new Seg[sectionPoints.Length];
            int i = 0;
            foreach (RouteSection section in routeBuilder.Sections)
            {
                MapPoint[] points = sectionPoints[i];
                _segments[i] = (section, points, points.Select((point, index) => (Point: point, Index: index)).ToDictionary(tuple => tuple.Point, tuple => tuple.Index));
                i++;
            }
            routeBuilder.CalculationFinished += RouteBuilder_CalculationFinished;
        }

        private void RouteBuilder_CalculationFinished(RouteSection section, RouteResult result)
        {
            if (!section.IsCanceled)
            {
                Seg segment = Array.Find(_segments, segment => segment.Section == section);
                MapPoint[] points = segment.Points;
                int i = 0;

                bool isDiff = false;
                int diffStartIndex;

                foreach (TrackPoint point in result.Points)
                {
                    if (i >= points.Length)
                    {
                        break;
                    }

                    if (!isDiff)
                    {
                        MapPoint originalPoint = points[i];
                        if (originalPoint.Latitude != point.Latitude || originalPoint.Longitude != point.Longitude)
                        {
                            isDiff = true;
                            diffStartIndex = i;
                        }
                    }

                    if (isDiff && segment.Indices.TryGetValue(new MapPoint(point.Latitude, point.Longitude), out int newIndex) && newIndex >= i)
                    {
                        // Diff von diffStartIndex - 1 bis newIndex
                        i = newIndex;
                        isDiff = false;
                    }

                    if (!isDiff)
                    {
                        i++;
                    }
                }
            }
        }
    }
}