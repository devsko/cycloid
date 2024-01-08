using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using cycloid.Routing;

namespace cycloid;

partial class Track
{
    private class SegmentCollection(PointCollection points) : IEnumerable<Segment>
    {
        private readonly PointCollection _points = points;
        private readonly List<Segment> _segments = [];

        public int Count => _segments.Count;

        public Segment this[int index] => _segments[index];

        public void Add(TrackPoint[] points) => Insert(_segments.Count, new Segment { Points = points });

        public Segment Find(RouteSection section) => _segments.Find(segment => segment.Section == section);

        public void Insert(int index, RouteSection section)
        {
            Segment segment = new() { Section = section };
            Insert(index, segment);
        }

        private void Insert(int index, Segment segment)
        {
            _segments.Insert(index, segment);
            if (index == 0)
            {
                segment.Linked = true;
            }
            else
            {
                LinkSegments(segment, _segments[index - 1]);
            }

            if (segment.Points is { })
            {
                _points.Total += segment.Values;
                LinkRemainingSegments(segment, index + 1);
            }
            else
            {
                UnlinkRemainingSegments(index + 1);
            }
        }

        public void Update(RouteSection section, RouteResult result)
        {
            if (!section.IsCanceled && result.IsValid)
            {
                Segment segment = Find(section);
                _points.Total -= segment.Values;
                segment.Points = TrackPointConverter.Convert(result.Points, result.PointCount);
                _points.Total += segment.Values;

                LinkRemainingSegments(segment, _segments.IndexOf(segment) + 1);
            }

            CheckTotal();
            CheckLinks();
        }

        public void RemoveAt(int index)
        {
            Segment segment = _segments[index];
            _segments.RemoveAt(index);
            _points.Total -= segment.Values;
            segment.Section = null;

            if (_segments.Count > 0)
            {
                if (index == 0)
                {
                    segment = _segments[0];
                    segment.StartIndex = 0;
                    segment.Start = default;
                    segment.Linked = true;
                }
                else
                {
                    segment = _segments[--index];
                }

                LinkRemainingSegments(segment, index + 1);
            }

            CheckTotal();
            CheckLinks();
        }

        private void LinkRemainingSegments(Segment previous, int index)
        {
            for (int i = index; i < _segments.Count; i++)
            {
                Segment segment = _segments[i];
                if (!LinkSegments(segment, previous))
                {
                    break;
                }
                previous = segment;
            }
        }

        private void UnlinkRemainingSegments(int index)
        {
            for (int i = index; i < _segments.Count; i++)
            {
                Segment segment = _segments[i];
                if (!segment.Linked)
                {
                    break;
                }
                segment.StartIndex = 0;
                segment.Start = default;
                segment.Linked = false;
            }
        }

        private bool LinkSegments(Segment segment, Segment previous)
        {
            if (previous.Points is null || !previous.Linked)
            {
                return false;
            }

            segment.StartIndex = previous.StartIndex + previous.Points.Length;
            segment.Start = previous.Start + previous.Values;
            segment.Linked = true;

            return segment.Points is { };
        }

        public IEnumerator<Segment> GetEnumerator() => _segments.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _segments.GetEnumerator();

        [Conditional("DEBUG")]
        private void CheckTotal()
        {
            Debug.Assert(_points.Total.Distance == _segments.Aggregate(default(TrackPoint.CommonValues), (acc, segment) => acc + segment.Values, acc => acc.Distance));
        }

        [Conditional("DEBUG")]
        private void CheckLinks()
        {
            bool shouldBeLinked = true;
            TrackPoint.CommonValues values = default;
            int index = 0;
            foreach (Segment segment in _segments)
            {
                if (segment.Linked != shouldBeLinked)
                {
                    Debug.Fail("Linkage");
                }
                if (shouldBeLinked)
                {
                    if (segment.Start.Distance != values.Distance)
                    {
                        Debug.Fail("Linkage");
                    }
                    if (segment.Points is { Length: > 0 } points)
                    {
                        values += segment.Values;
                    }
                    else
                    {
                        shouldBeLinked = false;
                    }
                }
                index++;
            }
        }
    }
}