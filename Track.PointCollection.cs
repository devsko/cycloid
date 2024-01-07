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
        
        [ObservableProperty]
        private TrackPoint.CommonValues _total;

        public PointCollection(RouteBuilder routeBuilder)
        {
            _routeBuilder = routeBuilder;
            _segments = new SegmentCollection(this);

            routeBuilder.SectionAdded += RouteBuilder_SectionAdded;
            routeBuilder.SectionRemoved += RouteBuilder_SectionRemoved;
            routeBuilder.CalculationFinished += RouteBuilder_CalculationFinished;
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

        public async Task<(WayPoint[], TrackPoint[][])> GetSegmentsAsync(CancellationToken cancellationToken)
        {
            using (await _routeBuilder.ChangeLock.EnterAsync(cancellationToken))
            {
                return 
                    (
                        _segments
                            .Select(segment => segment.Section.End)
                            .Prepend(_segments[0].Section.Start)
                            .ToArray(),
                        _segments
                            .Select(segment => segment.Points)
                            .ToArray()
                    );
            }
        }

        public IEnumerator<TrackPoint> GetEnumerator()
        {
            foreach ((_, TrackPoint point, _) in Enumerate())
            {
                yield return point;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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

        private void RouteBuilder_SectionAdded(RouteSection section, int index)
        {
            _segments.Insert(index, section);
        }

        private void RouteBuilder_SectionRemoved(RouteSection _, int index)
        {
            _segments.RemoveAt(index);
        }

        private void RouteBuilder_CalculationFinished(RouteSection section, RouteResult result)
        {
            _segments.Update(section, result);
        }
    }
}