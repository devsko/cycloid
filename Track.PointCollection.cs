using CommunityToolkit.Mvvm.ComponentModel;
using cycloid.Routing;
using System.Collections.Generic;
using System.Linq;

namespace cycloid;

partial class Track
{
    public partial class PointCollection : ObservableObject
    {
        [ObservableProperty]
        private TrackPoint.CommonValues _total;

        private SegmentCollection _segments;

        public PointCollection(RouteBuilder routeBuilder)
        {
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

                return lastSegment.StartIndex + lastSegment.Points.Length;
            }
        }

        public IEnumerable<TrackPoint[]> Segments => _segments.Select(segment => segment.Points);

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