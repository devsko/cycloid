using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using cycloid.Routing;

namespace cycloid;

partial class Track
{
    public partial class PointCollection : ObservableObject, IEnumerable<TrackPoint>,
        IRecipient<SectionAdded>,
        IRecipient<SectionRemoved>,
        IRecipient<CalculationFinished>,
        IRecipient<FileSplitChanged>
    {
        private readonly Track _track;
        private readonly SegmentCollection _segments;
        
        private TrackPoint.CommonValues _total;
        public TrackPoint.CommonValues Total
        {
            get => _total;
            private set => SetProperty(ref _total, value);
        }

        private float _minAltitude = float.PositiveInfinity;
        public float MinAltitude
        {
            get => _minAltitude;
            private set => SetProperty(ref _minAltitude, value);
        }

        private float _maxAltitude = float.NegativeInfinity;
        public float MaxAltitude
        {
            get => _maxAltitude;
            private set => SetProperty(ref _maxAltitude, value);
        }

        public PointCollection(Track track)
        {
            _track = track;
            _segments = new SegmentCollection(this);

            StrongReferenceMessenger.Default.Register<SectionAdded>(this);
            StrongReferenceMessenger.Default.Register<SectionRemoved>(this);
            StrongReferenceMessenger.Default.Register<CalculationFinished>(this);
            StrongReferenceMessenger.Default.Register<FileSplitChanged>(this);
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

        public Index LastIndex()
        {
            int segmentIndex = _segments.Count - 1;
            return new Index(segmentIndex, _segments[segmentIndex].Points.Length - 1);
        }

        public TrackPoint Last()
        {
            Segment segment = _segments[^1];
            return GetPoint(segment, segment.Points[^1]);
        }

        public (int FileId, float distance) FilePosition(float distance)
        {
            (_, Index index) = Search(distance);
            
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

            return (fileId, fileSplitDistance - distance);
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

        private struct GetNearestComparer : IComparer<(float Fraction, float Distance)>
        {
            public int Compare((float Fraction, float Distance) x, (float Fraction, float Distance) y) => x.Distance.CompareTo(y.Distance);
        }

        public TrackPoint GetNearestPoint<T>(T point, (MapPoint NorthWest, MapPoint SouthEast) region) where T : IMapPoint
        {
            (float Fraction, float Distance) GetDistance(((Segment Segment, TrackPoint TrackPoint, Index Index) Previous, (Segment Segment, TrackPoint TrackPoint, Index Index) Next) current)
            {
                bool IsInside(TrackPoint point)
                    => point.Latitude <= region.NorthWest.Latitude && point.Latitude >= region.SouthEast.Latitude &&
                        point.Longitude >= region.NorthWest.Longitude && point.Longitude <= region.SouthEast.Longitude;
    
                TrackPoint previousPoint = current.Previous.TrackPoint;
                TrackPoint nextPoint = current.Next.TrackPoint;
                if (IsInside(previousPoint) || IsInside(nextPoint))
                {
                    return GeoCalculation.MinimalDistance(previousPoint, nextPoint, point);
                }

                return (-1, float.PositiveInfinity);
            }

            (
                (
                    (Segment Segment, TrackPoint TrackPoint, Index Index) Previous, 
                    (Segment Segment, TrackPoint TrackPoint, Index Index) Next) nearest, 
                (float Fraction, float Distance) result) 
                = Enumerate()
                .InPairs()
                .MinByWithKey(GetDistance, new GetNearestComparer());

            if (float.IsInfinity(result.Distance))
            {
                return TrackPoint.Invalid;
            }
                
            return TrackPoint.Lerp(GetPoint(nearest.Previous.Segment, nearest.Previous.TrackPoint), GetPoint(nearest.Next.Segment, nearest.Next.TrackPoint), result.Fraction);
        }

        public (TrackPoint Point, float Distance)[] GetNearPoints(MapPoint location, float maxDistance, int minDistanceDelta)
        {
            IEnumerator<(Segment Segment, TrackPoint Point, Index _)> enumerator = Enumerate().GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return [];
            }

            List<(TrackPoint Point, float Distance)> results = [];
            (TrackPoint Point, float Distance)? currentResult = null;
            float? lastExitTrackDistance = null;

            (Segment previousSegment, TrackPoint previousPoint, _) = enumerator.Current;
            while (enumerator.MoveNext())
            {
                (Segment currentSegment, TrackPoint currentPoint, _) = enumerator.Current;

                (float? fraction, float testDistance) = GeoCalculation.MinimalDistance(previousPoint, currentPoint, location);

                if (currentResult is not null)
                {
                    if (testDistance < currentResult.Value.Distance)
                    {
                        currentResult = (TrackPoint.Lerp(GetPoint(previousSegment, previousPoint), GetPoint(currentSegment, currentPoint), fraction.Value), testDistance);
                        lastExitTrackDistance = null;
                    }
                    else if (testDistance > maxDistance)
                    {
                        float trackDistance = currentSegment.Start.Distance + currentPoint.Distance;
                        if (lastExitTrackDistance is null)
                        {
                            lastExitTrackDistance = trackDistance;
                        }
                        else if (trackDistance > lastExitTrackDistance.Value + minDistanceDelta)
                        {
                            results.Add(currentResult.Value);
                            currentResult = null;
                            lastExitTrackDistance = null;
                        }
                    }
                    else
                    {
                        lastExitTrackDistance = null;
                    }
                }
                else if (testDistance <= maxDistance)
                {
                    currentResult = (TrackPoint.Lerp(GetPoint(previousSegment, previousPoint), GetPoint(currentSegment, currentPoint), fraction.Value), testDistance);
                }
                (previousSegment, previousPoint) = (currentSegment, currentPoint);
            }
            if (currentResult is not null)
            {
                results.Add(currentResult.Value);
            }

            return [.. results];
        }

        public async Task<(WayPoint[] WayPoints, TrackPoint[][] TrackPoints)> GetSegmentsAsync(CancellationToken cancellationToken)
        {
            using (await _track.RouteBuilder.ChangeLock.EnterAsync(cancellationToken))
            {
                return 
                (
                    _track.RouteBuilder.Points.ToArray(),
                    _segments.Select(segment => segment.Points).ToArray()
                );
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

        // TODO return (Segment, TrackPoint, Index)
        private Index GetIndex(float distance, out bool exactMatch, int step = 1)
        {
            if (_segments.Count == 0)
            {
                exactMatch = true;
                return Index.Invalid;
            }

            // TODO -> Array.BinarySearch in SegmentCollection.Search
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
                    // TODO wann passiert das? Besser den richtigen Punkt im Segment davor?
                    pointIndex += step;
                }
            }

            return new Index(segmentIndex, pointIndex);
        }

        private Index Decrement(Index index) => 
            index == default
            ? Index.Invalid
            : index.PointIndex == 0
            ? new Index(index.SegmentIndex - 1, _segments[index.SegmentIndex - 1].Points.Length - 2) // Skip last point in all but last segment
            : index with { PointIndex = index.PointIndex - 1 };

        private Index Increment(Index index) => 
            index == LastIndex()
            ? Index.Invalid
            : index.PointIndex == _segments[index.SegmentIndex].Points.Length - 2 // Skip last point in all but last segment
            ? new Index(index.SegmentIndex + 1, 0)
            : index with { PointIndex = index.PointIndex + 1 };

        private IEnumerable<(Segment Segment, TrackPoint Point, Index Index)> Enumerate(Index from = default)
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

        void IRecipient<SectionAdded>.Receive(SectionAdded message)
        {
            _segments.Insert(new Segment { Section = message.Section }, message.Index);
        }

        void IRecipient<SectionRemoved>.Receive(SectionRemoved message)
        {
            Segment segment = _segments[message.Index];
            Total -= segment.Values;

            _segments.Remove(segment, message.Index);

            if (MinAltitude == segment.MinAltitude || MaxAltitude == segment.MaxAltitude)
            {
                CalculateMinMaxAltitude();
            }
        }

        void IRecipient<CalculationFinished>.Receive(CalculationFinished message)
        {
            if (!message.Section.IsCanceled)
            {
                Segment segment = _segments.Find(message.Section);

                Total -= segment.Values;
                bool calcMinAltitude = MinAltitude == segment.MinAltitude;
                bool calcMaxAltitude = MaxAltitude == segment.MaxAltitude;

                RouteResult result = message.Result;
                if (!result.IsValid)
                {
                    result = TrackPointConverter.Convert(
                        RoutePoint.FromMapPoint(message.Section.Start.Location, 0, TimeSpan.Zero),
                        RoutePoint.FromMapPoint(message.Section.End.Location, 0, TimeSpan.FromHours(message.Section.DirectDistance / 1_000 / 20)));
                }

                (segment.Points, segment.MinAltitude, segment.MaxAltitude) = (result.Points, result.MinAltitude, result.MaxAltitude);

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
        }

        void IRecipient<FileSplitChanged>.Receive(FileSplitChanged message)
        {
            if (message.WayPoint != _track.RouteBuilder.Points[^1])
            {
                _segments.UpdateFileId(_segments.Find(message.WayPoint));
            }
        }
    }
}