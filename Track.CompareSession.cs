using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using cycloid.Routing;

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
        private readonly struct OriginalSegment
        {
            public TrackPoint[] Points { get; }
            public Dictionary<MapPoint, int> Indices { get; }

            public OriginalSegment(TrackPoint[] points)
            {
                Points = points;
                Indices = points.Select((point, index) => (Point: (MapPoint)point, Index: index)).ToDictionary(tuple => tuple.Point, tuple => tuple.Index);
            }
        }

        private class NewSegment
        {
            public MapPoint Start { get; init; }
            public TrackPoint[] Points { get; set; }
        }

        private readonly Track _track;
        private readonly OriginalSegment[] _originalSegments;
        private readonly Dictionary<MapPoint, int> _originalSegmentIndices;
        private readonly List<NewSegment> _newSegments;

        public ObservableCollection<TrackDifference> Differences { get; } = [];

        public CompareSession(Track track, (WayPoint[] WayPoints, TrackPoint[][] TrackPoints) segments)
        {
            _track = track;
            _originalSegments = segments.TrackPoints.Select(points => new OriginalSegment(points)).ToArray();
            _originalSegmentIndices = segments.WayPoints.SkipLast(1).Select((point, index) => (point, index)).ToDictionary(tuple => tuple.point.Location, tuple => tuple.index);
            _newSegments = segments.WayPoints.SkipLast(1).Zip(segments.TrackPoints, (wayPoint, points) => new NewSegment { Start = wayPoint.Location, Points = points }).ToList();

            _track.RouteBuilder.CalculationStarting += RouteBuilder_CalculationStarting;
            _track.RouteBuilder.CalculationFinished += RouteBuilder_CalculationFinished;
            _track.RouteBuilder.SectionAdded += RouteBuilder_SectionAdded;
            _track.RouteBuilder.SectionRemoved += RouteBuilder_SectionRemoved;
            _track.RouteBuilder.Changed += RouteBuilder_Changed;
            _track.RouteBuilder.FileSplitChanged += RouteBuilder_FileSplitChanged;
        }

        public void Rollback()
        {
            Differences.Clear();

            // TODO alle Section.Cancel()
            // TODO Alles wieder mit den originalen Points initialisieren
        }

        private void RouteBuilder_FileSplitChanged(WayPoint obj)
        {
        }

        private void RouteBuilder_Changed(bool obj)
        {
        }

        private void RouteBuilder_SectionRemoved(RouteSection section, int index)
        {
            _newSegments.RemoveAt(index);
            // TODO Wenn der erste/letzte WayPoint entfernt wird, folgt keine Kalkulation aber die Diffs müssen trotzdem neu gefunden werden
        }

        private void RouteBuilder_SectionAdded(RouteSection section, int index)
        {
            _newSegments.Insert(index, new NewSegment { Start = section.Start.Location });
        }

        private void RouteBuilder_CalculationStarting(RouteSection section)
        {
            _newSegments[_track.RouteBuilder.GetSectionIndex(section)].Points = null;
        }

        private void RouteBuilder_CalculationFinished(RouteSection section, RouteResult result)
        {
            if (!section.IsCanceled && result.IsValid)
            {
                int newIndex = _track.RouteBuilder.GetSectionIndex(section);
                _newSegments[newIndex].Points = result.Points;

                int newStartIndex = newIndex, originalStartIndex = 0;
                while (true)
                {
                    NewSegment newSegment = _newSegments[newStartIndex];
                    if (newSegment.Points is null)
                    {
                        return;
                    }
                    if (_originalSegmentIndices.TryGetValue(newSegment.Start, out originalStartIndex) || newStartIndex == 0)
                    {
                        break;
                    }
                    newStartIndex--;
                }

                int newEndIndex = newIndex, originalEndIndex = 0;
                while (true)
                {
                    if (_newSegments[newEndIndex].Points is null)
                    {
                        return;
                    }
                    if (newEndIndex == _newSegments.Count - 1)
                    {
                        // We do not test if the new / original end points are equal because it does not matter
                        originalEndIndex = _originalSegments.Length;
                        break;
                    }
                    if (_originalSegmentIndices.TryGetValue(_newSegments[newEndIndex + 1].Start, out originalEndIndex))
                    {
                        break;
                    }
                    newEndIndex++;
                }
                originalEndIndex--;
            }

            //if (result.IsValid)
            //{
            //    Seg segment = Array.Find(_segments, segment => segment.Section == section);
            //    TrackPoint[] points = segment.Points;
            //    int i = 0;

            //    bool isDiff = false;
            //    TrackPoint.CommonValues diffStart = default;
            //    int diffStartIndex = default;

            //    List<TrackDifference> differences = new();
            //    foreach (TrackPoint point in result.Points)
            //    {
            //        if (i >= points.Length)
            //        {
            //            break;
            //        }

            //        if (!isDiff)
            //        {
            //            if (!points[i].LocationEquals(point))
            //            {
            //                isDiff = true;
            //                diffStartIndex = Math.Max(0, i - 1);
            //            }
            //        }

            //        if (isDiff && segment.Indices.TryGetValue(point, out int newIndex) && newIndex >= i)
            //        {
            //            TrackPoint.CommonValues newValues = point.Values - diffStart;
            //            TrackPoint.CommonValues originalValues = points[newIndex].Values - points[diffStartIndex].Values;

            //            differences.Add(new TrackDifference
            //            {
            //                Section = section,
            //                OriginalPoints = new ArraySegment<TrackPoint>(points, diffStartIndex, newIndex - diffStartIndex + 1),
            //                Length = newValues.Distance,
            //                DistanceDiff = newValues.Distance - originalValues.Distance,
            //                AscentDiff = newValues.Ascent - originalValues.Ascent,
            //            });

            //            i = newIndex;
            //            isDiff = false;
            //        }

            //        if (!isDiff)
            //        {
            //            diffStart = point.Values;
            //            i++;
            //        }
            //    }

            //    CommitDifferences();

            //    void CommitDifferences()
            //    {
            //        if (differences.Count > 0)
            //        {
            //            int sectionIndex = _track.RouteBuilder.GetSectionIndex(section);
            //            int i = 0;
            //            while (i < Differences.Count && _track.RouteBuilder.GetSectionIndex(Differences[i].Section) < sectionIndex)
            //            {
            //                i++;
            //            }
            //            for (int j = 0; j < differences.Count; j++)
            //            {
            //                Differences.Insert(i + j, differences[j]);
            //            }
            //        }
            //    }
            //}
        }
    }
}