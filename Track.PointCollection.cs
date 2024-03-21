using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using cycloid.Routing;

namespace cycloid;

partial class Track
{
    public partial class PointCollection : ObservableObject, IEnumerable<TrackPoint>
    {
        private readonly RouteBuilder _routeBuilder;
        private readonly SegmentCollection _segments;
        
        private TrackPoint.CommonValues _total;
        private float _minAltitude = float.PositiveInfinity;
        private float _maxAltitude = float.NegativeInfinity;

        public PointCollection(RouteBuilder routeBuilder)
        {
            _routeBuilder = routeBuilder;
            _segments = new SegmentCollection(this);

            routeBuilder.SectionAdded += RouteBuilder_SectionAdded;
            routeBuilder.SectionRemoved += RouteBuilder_SectionRemoved;
            routeBuilder.CalculationFinished += RouteBuilder_CalculationFinished;
            routeBuilder.FileSplitChanged += RouteBuilder_FileSplitChanged;
        }

        public TrackPoint.CommonValues Total
        {
            get => _total;
            private set => SetProperty(ref _total, value);
        }

        public float MinAltitude
        {
            get => _minAltitude;
            private set => SetProperty(ref _minAltitude, value);
        }

        public float MaxAltitude
        {
            get => _maxAltitude;
            private set => SetProperty(ref _maxAltitude, value);
        }

        public TrackPoint this[Index index]
        {
            get
            {
                Segment segment = _segments[index.SegmentIndex];
                TrackPoint point = segment.Points[index.PointIndex];

                return GetPoint(segment, point);
            }
        }

        public int Count
        {
            get
            {
                if (_segments.Count == 0)
                {
                    return 0;
                }

                Segment lastSegment = _segments[^1];

                return lastSegment.StartIndex - _segments.Count + lastSegment.Points.Length;
            }
        }

        public (int FileId, float distance) FilePosition(TrackPoint point)
        {
            (_, Index index) = Search(point.Distance);
            
            int fileId = _segments[index.SegmentIndex].FileId;

            float fileSplitDistance = Total.Distance;
            for (int i = index.SegmentIndex + 1; i < _segments.Count; i++)
            {
                Segment segment = _segments[i];
                if (segment.FileId != fileId)
                {
                    fileSplitDistance = segment.Start.Distance;
                    break;
                }
            }

            return (fileId, fileSplitDistance - point.Distance);
        }

        public (TrackPoint Point, Index Index) Search(float distance)
        {
            Index index = GetIndex(distance, out bool exactMatch);
            if (!index.IsValid)
            {
                return (TrackPoint.Invalid, index);
            }

            Segment segment = _segments[index.SegmentIndex];

            if (index.PointIndex >= segment.Points.Length)
            {
                index = index with { PointIndex = segment.Points.Length - 1 };
                exactMatch = true;
            }

            TrackPoint point = this[index];

            if (!exactMatch && index != default)
            {
                TrackPoint previous = this[Decrement(index)];
                point = TrackPoint.Lerp(previous, point, (distance - previous.Distance) / (point.Distance - previous.Distance));
            }

            return (point, index);
        }

        public async Task<(WayPoint[], TrackPoint[][])> GetSegmentsAsync(CancellationToken cancellationToken)
        {
            using (await _routeBuilder.ChangeLock.EnterAsync(cancellationToken))
            {
                return 
                (
                    _routeBuilder.Points.ToArray(),
                    _segments.Select(segment => segment.Points).ToArray()
                );
            }
        }

        public async Task<CompareSession> StartCompareSessionAsync()
        {
            using (await _routeBuilder.ChangeLock.EnterAsync(default))
            {
                MapPoint[][] segmentPoints = new MapPoint[_segments.Count][];
                int i = 0;
                foreach (Segment segment in _segments)
                {
                    segmentPoints[i] = segment.Points.Select(point => new MapPoint(point.Latitude, point.Longitude)).ToArray();
                    i++;
                }

                return new CompareSession(_routeBuilder, segmentPoints);
            }
        }

        public IEnumerator<TrackPoint> GetEnumerator()
        {
            foreach ((Segment segment, TrackPoint point, _) in Enumerate())
            {
                yield return GetPoint(segment, point);
            }
        }

        public IEnumerable<(float Distance, float Altitude)> Enumerate(float fromDistance, float toDistance, int step = 1)
        {
            IEnumerator<(Segment Segment, TrackPoint Point, Index index)> enumerator = Enumerate(GetIndex(fromDistance, out _, step)).GetEnumerator();
            if (enumerator.MoveNext())
            {
                while (true)
                {
                    (Segment segment, TrackPoint point, _) = enumerator.Current;
                    float distance = segment.Start.Distance + point.Distance;
                    if (distance > toDistance)
                    {
                        break;
                    }

                    yield return (distance, point.Altitude);

                    for (int i = 0; i < step; i++)
                    {
                        if (!enumerator.MoveNext())
                        {
                            yield break;
                        }
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private Index GetIndex(float distance, out bool exactMatch, int step = 1)
        {
            if (_segments.Count == 0)
            {
                exactMatch = true;
                return Index.Invalid;
            }

            int segmentIndex = 0;
            for (; segmentIndex < _segments.Count; segmentIndex++)
            {
                if (_segments[segmentIndex].Start.Distance > distance)
                {
                    break;
                }
            }
            segmentIndex--;

            Segment segment = _segments[segmentIndex];
            TrackPoint[] points = segment.Points;

            int pointIndex = Array.BinarySearch(points, new TrackPoint(0, 0, 0, distance: distance - segment.Start.Distance), TrackPoint.DistanceComparer.Instance);
            if (pointIndex >= 0)
            {
                exactMatch = true;
            }
            else
            {
                exactMatch = false;
                pointIndex = ~pointIndex;
            }

            if (step != 1)
            {
                pointIndex = (pointIndex + segment.StartIndex) / step * step - segment.StartIndex;
                if (pointIndex < 0)
                {
                    pointIndex += step;
                }
            }

            return new Index(segmentIndex, pointIndex);
        }

        private Index Decrement(Index index) => index.PointIndex == 0
            ? new Index(index.SegmentIndex - 1, _segments[index.SegmentIndex - 1].Points.Length - 1)
            : index with { PointIndex = index.PointIndex - 1 };

        private IEnumerable<(Segment Segment, TrackPoint Point, Index index)> Enumerate(Index from = default)
        {
            int startPointIndex = from.PointIndex;
            for (int segmentIndex = from.SegmentIndex; segmentIndex < _segments.Count; segmentIndex++)
            {
                Segment segment = _segments[segmentIndex];
                TrackPoint[] points = segment.Points;
                for (int pointIndex = startPointIndex; pointIndex < points.Length - 1; pointIndex++)
                {
                    yield return (segment, points[pointIndex], new Index(segmentIndex, pointIndex));
                }
                startPointIndex = 0;
            }
        }

        private static TrackPoint GetPoint(Segment segment, TrackPoint point) => point with { Values = segment.Start + point.Values };

        private void CalculateMinMaxAltitude()
        {
            (MinAltitude, MaxAltitude) = _segments.Aggregate(
                (float.PositiveInfinity, float.NegativeInfinity),
                ((float Min, float Max) acc, Segment segment) => (
                    Math.Min(acc.Min, segment.MinAltitude),
                    Math.Max(acc.Max, segment.MaxAltitude)));
        }

        private void RouteBuilder_SectionAdded(RouteSection section, int index)
        {
            _segments.Insert(new Segment { Section = section }, index);
        }

        private void RouteBuilder_SectionRemoved(RouteSection _, int index)
        {
            Segment segment = _segments[index];
            
            Total -= segment.Values;
            
            _segments.Remove(segment, index);
            
            if (MinAltitude == segment.MinAltitude || MaxAltitude == segment.MaxAltitude)
            {
                CalculateMinMaxAltitude();
            }
        }

        private void RouteBuilder_CalculationFinished(RouteSection section, RouteResult result)
        {
            if (!section.IsCanceled)
            {
                Segment segment = _segments.Find(section);

                Total -= segment.Values;
                bool calcMinAltitude = MinAltitude == segment.MinAltitude;
                bool calcMaxAltitude = MaxAltitude == segment.MaxAltitude;

                (segment.Points, segment.MinAltitude, segment.MaxAltitude) =
                    result.IsValid
                    ? TrackPointConverter.Convert(result.Points, result.PointCount)
                    : ConvertDirectRoute(section);

                Total += segment.Values;
                if (segment.MinAltitude < MinAltitude)
                {
                    MinAltitude = segment.MinAltitude;
                    calcMinAltitude = false;
                }
                if (segment.MaxAltitude > MaxAltitude)
                {
                    MaxAltitude = segment.MaxAltitude;
                    calcMaxAltitude = false;
                }
                if (calcMinAltitude || calcMaxAltitude)
                {
                    CalculateMinMaxAltitude();
                }

                _segments.Update(segment);
            }

            static (TrackPoint[] points, float minAltitude, float maxAltitude) ConvertDirectRoute(RouteSection section)
            {
                return TrackPointConverter.Convert(
                [
                    new RoutePoint(section.Start.Location.Latitude, section.Start.Location.Longitude, 0, TimeSpan.Zero),
                    new RoutePoint(section.End.Location.Latitude, section.End.Location.Longitude, 0, TimeSpan.FromHours(section.DirectDistance / 1_000 / 20))
                ], 2);
            }
        }

        private void RouteBuilder_FileSplitChanged(WayPoint wayPoint)
        {
            if (wayPoint != _routeBuilder.Points[^1])
            {
                _segments.UpdateFileId(_segments.Find(wayPoint));
            }
        }
    }
}