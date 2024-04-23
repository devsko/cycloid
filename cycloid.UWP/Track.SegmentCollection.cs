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

        public int FindIndex(RouteSection section) => _segments.FindIndex(segment => segment.Section == section);

        public Segment Find(RouteSection section) => _segments.Find(segment => segment.Section == section);
        
        public Segment Find(WayPoint wayPoint) => _segments.Find(segment => segment.Section.Start == wayPoint);

        public void Insert(Segment segment, int index)
        {
            _segments.Insert(index, segment);
            if (index == 0)
            {
                segment.FileId = 1;
                segment.Linked = true;
            }
            else
            {
                LinkSegments(segment, _segments[index - 1]);
            }

            UnlinkRemainingSegments(index + 1);
        }

        public void Update(Segment segment)
        {
            LinkRemainingSegments(segment, _segments.IndexOf(segment) + 1);

            CheckTotal();
            CheckLinks();
        }

        public void Remove(Segment segment, int index)
        {
            _segments.RemoveAt(index);
            segment.Section = null;

            if (_segments.Count > 0)
            {
                if (index == 0)
                {
                    segment = _segments[0];
                    segment.StartIndex = 0;
                    segment.Start = default;
                    segment.FileId = 1;
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

        public void UpdateFileId(Segment segment)
        {
            int index = _segments.IndexOf(segment);
            Segment previous;
            int newFileId;
            if (index == 0)
            {
                newFileId = 1;
            }
            else
            {
                previous = _segments[index - 1];
                newFileId = previous.FileId + (segment.Section.Start.IsFileSplit ? 1 : 0);
            }
            int diff = newFileId - segment.FileId;
            if (diff != 0)
            {
                for (int i = index; i < _segments.Count; i++)
                {
                    _segments[i].FileId += diff;
                }
            }
        }

        public IEnumerator<Segment> GetEnumerator() => _segments.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _segments.GetEnumerator();

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
                segment.StartIndex = -1;
                segment.Start = default;
                segment.FileId = 0;
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
            segment.FileId = previous.FileId + (segment.Section.Start.IsFileSplit ? 1 : 0);
            segment.Linked = true;

            return segment.Points is { };
        }

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