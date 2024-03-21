using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using cycloid.Routing;

using Seg = (cycloid.Routing.RouteSection Section, cycloid.TrackPoint[] Points, System.Collections.Generic.Dictionary<cycloid.MapPoint, int> Indices);

namespace cycloid;

public class TrackDifference
{
    public RouteSection Section { get; init; }
    public ArraySegment<TrackPoint> OriginalPoints { get; init; }
    public float Length { get; init; }
    public float DistanceDiff { get; init; }
    public float AscentDiff { get; init; }
}

partial class Track
{
    public class CompareSession
    {
        private readonly RouteBuilder _routeBuilder;
        private readonly Seg[] _segments;

        public ObservableCollection<TrackDifference> Differences { get; } = new();

        public CompareSession(RouteBuilder routeBuilder, TrackPoint[][] sectionPoints)
        {
            _routeBuilder = routeBuilder;
            _segments = new Seg[sectionPoints.Length];
            int i = 0;
            foreach (RouteSection section in routeBuilder.Sections)
            {
                TrackPoint[] points = sectionPoints[i];
                _segments[i] = (section, points, points.Select((point, index) => (Point: (MapPoint)point, Index: index)).ToDictionary(tuple => tuple.Point, tuple => tuple.Index));
                i++;
            }
            routeBuilder.CalculationFinished += RouteBuilder_CalculationFinished;
        }

        private void RouteBuilder_CalculationFinished(RouteSection section, RouteResult result)
        {
            if (result.IsValid)
            {
                Seg segment = Array.Find(_segments, segment => segment.Section == section);
                TrackPoint[] points = segment.Points;
                int i = 0;

                bool isDiff = false;
                TrackPoint.CommonValues diffStart = default;
                int diffStartIndex = default;

                List<TrackDifference> differences = new();
                foreach (TrackPoint point in result.Points)
                {
                    if (i >= points.Length)
                    {
                        break;
                    }

                    if (!isDiff)
                    {
                        if (!points[i].LocationEquals(point))
                        {
                            isDiff = true;
                            diffStartIndex = Math.Max(0, i - 1);
                        }
                    }

                    if (isDiff && segment.Indices.TryGetValue(point, out int newIndex) && newIndex >= i)
                    {
                        TrackPoint.CommonValues newValues = point.Values - diffStart;
                        TrackPoint.CommonValues originalValues = points[newIndex].Values - points[diffStartIndex].Values;

                        differences.Add(new TrackDifference
                        {
                            Section = section,
                            OriginalPoints = new ArraySegment<TrackPoint>(points, diffStartIndex, newIndex - diffStartIndex + 1),
                            Length = newValues.Distance,
                            DistanceDiff = newValues.Distance - originalValues.Distance,
                            AscentDiff = newValues.Ascent - originalValues.Ascent,
                        });

                        i = newIndex;
                        isDiff = false;
                    }

                    if (!isDiff)
                    {
                        diffStart = point.Values;
                        i++;
                    }
                }

                CommitDifferences();

                void CommitDifferences()
                {
                    if (differences.Count > 0)
                    {
                        int sectionIndex = _routeBuilder.GetSectionIndex(section);
                        int i = 0;
                        while (i < Differences.Count && _routeBuilder.GetSectionIndex(Differences[i].Section) < sectionIndex)
                        {
                            i++;
                        }
                        for (int j = 0; j < differences.Count; j++)
                        {
                            Differences.Insert(i + j, differences[j]);
                        }
                    }
                }
            }
        }
    }
}