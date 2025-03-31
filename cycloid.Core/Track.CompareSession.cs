using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using cycloid.Routing;

using IndexedPoint = (cycloid.TrackPoint Point, int SegmentIndex, int PointIndex);

namespace cycloid;

public class CompareSessionChanged(object sender, CompareSession oldValue, CompareSession newValue) : PropertyChangedMessage<CompareSession>(sender, null, oldValue, newValue);

public class RemovingDifference((TrackDifference, int) value) : ValueChangedMessage<(TrackDifference Difference, int Index)>(value);

public class TrackDifference
{
    public IEnumerable<TrackPoint> OriginalPoints { get; init; }
    public int SectionIndex { get; init; }
    public float Length { get; init; }
    public TrackPoint.CommonValues Diff { get; init; }
}

partial class Track
{
    [ObservableProperty]
    public partial CompareSession CompareSession { get; set; }

    partial void OnCompareSessionChanged(CompareSession oldValue, CompareSession newValue)
    {
        StrongReferenceMessenger.Default.Send(new CompareSessionChanged(this, oldValue, newValue));
    }

    public async Task InitializeCompareSessionAsync()
    {
        CompareSession?.Initialize(await Points.GetSegmentsAsync(default));
    }
}

public class CompareSession : ObservableObject,
    IRecipient<SectionAdded>,
    IRecipient<SectionRemoved>,
    IRecipient<CalculationStarting>,
    IRecipient<CalculationFinished>
{
    private readonly struct OriginalSegment(TrackPoint[] points)
    {
        public TrackPoint[] Points { get; } = points;
        public Dictionary<MapPoint, int> Indices { get; } = points.Select((point, index) => (Point: (MapPoint)point, Index: index)).ToLookup(tuple => tuple.Point).ToDictionary(lookup => lookup.Key, lookup => lookup.First().Index);
    }

    private class NewSegment
    {
        public MapPoint Start { get; init; }
        public TrackPoint[] Points { get; set; }
    }

    private readonly Track _track;
    private readonly Profile _originalProfile;
    private readonly TrackPoint.CommonValues _originalValues;
    private readonly OriginalSegment[] _originalSegments;
    private readonly Dictionary<MapPoint, int> _originalSegmentIndices;
    private readonly WayPoint[] _originalWayPoints;
    private readonly List<NewSegment> _newSegments;

    public ObservableCollection<TrackDifference> Differences { get; } = [];

    public CompareSession(Track track, (WayPoint[] WayPoints, TrackPoint[][] TrackPoints) segments)
    {
        _track = track;
        _originalProfile = track.RouteBuilder.Profile;
        _originalValues = track.Points.Total;
        _originalSegments = segments.TrackPoints.Select(points => new OriginalSegment(points)).ToArray();
        _originalSegmentIndices = segments.WayPoints.SkipLast(1).Select((point, index) => (Point: point, Index: index)).ToLookup(tuple => tuple.Point.Location).ToDictionary(lookup => lookup.Key, lookup => lookup.First().Index);
        _originalWayPoints = segments.WayPoints;
        _newSegments = segments.WayPoints.SkipLast(1).Zip(segments.TrackPoints, (wayPoint, points) => new NewSegment { Start = wayPoint.Location, Points = points }).ToList();

        StrongReferenceMessenger.Default.Register<SectionAdded>(this);
        StrongReferenceMessenger.Default.Register<SectionRemoved>(this);
        StrongReferenceMessenger.Default.Register<CalculationStarting>(this);
        StrongReferenceMessenger.Default.Register<CalculationFinished>(this);
    }

    public CompareSession(Track track, Profile profile, TrackPoint.CommonValues values, WayPoint[] wayPoints, TrackPoint[][] trackPoints)
    {
        _track = track;
        _originalProfile = profile;
        _originalValues = values;
        _originalSegments = trackPoints.Select(segmentPoints => new OriginalSegment(segmentPoints)).ToArray();
        _originalSegmentIndices = wayPoints.SkipLast(1).Select((point, index) => (Point: point, Index: index)).ToLookup(tuple => tuple.Point.Location).ToDictionary(lookup => lookup.Key, lookup => lookup.First().Index);
        _originalWayPoints = wayPoints;
        _newSegments = [];
    }

    public void Initialize((WayPoint[] WayPoints, TrackPoint[][] TrackPoints) segments)
    {
        _newSegments.AddRange(segments.WayPoints.SkipLast(1).Zip(segments.TrackPoints, (wayPoint, points) => new NewSegment { Start = wayPoint.Location, Points = points }));

        for (int newIndex = 0; newIndex < _newSegments.Count; newIndex++)
        {
            CreateDifferences(newIndex);
        }

        StrongReferenceMessenger.Default.Register<SectionAdded>(this);
        StrongReferenceMessenger.Default.Register<SectionRemoved>(this);
        StrongReferenceMessenger.Default.Register<CalculationStarting>(this);
        StrongReferenceMessenger.Default.Register<CalculationFinished>(this);
    }

    public TrackPoint.CommonValues Diff => _track.Points.Total - _originalValues;

    public int OriginalSegmentsCount => _originalSegments.Length;

    public Profile OriginalProfile => _originalProfile;

    public IEnumerable<TrackPoint[]> OriginalTrackPoints => _originalSegments.Select(segment => segment.Points);

    public WayPoint[] OriginalWayPoints => _originalWayPoints;

    public TrackPoint.CommonValues OriginalValues => _originalValues;

    public async Task RollbackAsync()
    {
        Dispose();

        _track.RouteBuilder.Profile = _originalProfile;
        await _track.RouteBuilder.InitializeAsync(
            _originalWayPoints
                .Zip(
                    _originalSegments
                        .Select(segment => segment.Points.Select(RoutePoint.FromTrackPoint).ToArray())
                        .Prepend(null),
                    (wayPoint, points) => (wayPoint, points)));
    }

    public void Dispose()
    {
        StrongReferenceMessenger.Default.Unregister<SectionAdded>(this);
        StrongReferenceMessenger.Default.Unregister<SectionRemoved>(this);
        StrongReferenceMessenger.Default.Unregister<CalculationStarting>(this);
        StrongReferenceMessenger.Default.Unregister<CalculationFinished>(this);
    }

    private void CreateDifferences(int newIndex)
    {
        int newStart = newIndex, originalStart = 0;
        while (true)
        {
            NewSegment newSegment = _newSegments[newStart];
            if (newSegment.Points is null)
            {
                return;
            }
            if (_originalSegmentIndices.TryGetValue(newSegment.Start, out originalStart) || newStart == 0)
            {
                break;
            }
            newStart--;
        }

        int newEnd = newIndex, originalEnd = 0;
        while (true)
        {
            if (_newSegments[newEnd].Points is null)
            {
                return;
            }
            if (newEnd == _newSegments.Count - 1)
            {
                // We do not test if the new / original end points are equal because it does not matter
                originalEnd = _originalSegments.Length;
                break;
            }
            if (_originalSegmentIndices.TryGetValue(_newSegments[newEnd + 1].Start, out originalEnd))
            {
                break;
            }
            newEnd++;
        }
        originalEnd--;

        IEnumerator<IndexedPoint> originalEnumerator = EnumerateOriginalPoints(originalStart, 0, originalEnd, -1).GetEnumerator();
        bool hasMore = originalEnumerator.MoveNext();
        IndexedPoint original = originalEnumerator.Current;

        bool differs = false;
        List<TrackDifference> differences = [];

        IEnumerator<IndexedPoint> newEnumerator = EnumerateNewPoints(newStart, newEnd).GetEnumerator();
        IndexedPoint newPoint = default;
        IndexedPoint newDiffPoint = default;
        while (true)
        {
            if (!newEnumerator.MoveNext())
            {
                if (differs)
                {
                    while (originalEnumerator.MoveNext()) ;
                    AddDifference();
                }
                break;
            }

            newPoint = newEnumerator.Current;

            if (!hasMore)
            {
                break;
            }

            if (!differs)
            {
                if (!original.Point.Equals(newPoint.Point))
                {
                    newDiffPoint = newPoint;
                    differs = true;
                }
            }

            if (differs && TryGetOriginalPoint())
            {
                AddDifference();
                newDiffPoint = default;
                differs = false;
            }

            if (!differs)
            {
                hasMore = originalEnumerator.MoveNext();
                original = originalEnumerator.Current;
            }

            bool TryGetOriginalPoint()
            {
                for (int segmentIndex = original.SegmentIndex; segmentIndex <= originalEnd; segmentIndex++)
                {
                    if (_originalSegments[segmentIndex].Indices.TryGetValue(newPoint.Point, out int pointIndex) && (segmentIndex > original.SegmentIndex || pointIndex > original.PointIndex))
                    {
                        originalEnumerator = EnumerateOriginalPoints(segmentIndex, pointIndex, originalEnd, -1).GetEnumerator();
                        originalEnumerator.MoveNext();
                        return true;
                    }
                }
                return false;
            }

            void AddDifference()
            {
                (int originalStartSegmentIndex, int originalStartPointIndex) = PreviousIndex(original.SegmentIndex, original.PointIndex, originalStart);
                TrackPoint[] originalPoints = _originalSegments[originalStartSegmentIndex].Points;
                TrackPoint originalStartPoint = originalPoints[originalStartPointIndex == -1 ? originalPoints.Length - 2 : originalStartPointIndex];
                IndexedPoint originalEndPoint = originalEnumerator.Current;
                TrackPoint.CommonValues originalValues = GetOriginalValues(originalStartPoint, originalStartSegmentIndex, originalEndPoint.Point, originalEndPoint.SegmentIndex);

                (int newStartSegmentIndex, int newStartPointIndex) = PreviousIndex(newDiffPoint.SegmentIndex, newDiffPoint.PointIndex, newStart);
                TrackPoint[] newPoints = _newSegments[newStartSegmentIndex].Points;
                TrackPoint newStartPoint = newPoints[newStartPointIndex == -1 ? newPoints.Length - 2 : newStartPointIndex];
                IndexedPoint newEndPoint = newPoint;
                TrackPoint.CommonValues newValues = GetNewValues(newStartPoint, newStartSegmentIndex, newEndPoint.Point, newEndPoint.SegmentIndex);

                differences.Add(new TrackDifference
                {
                    OriginalPoints = EnumerateOriginalPoints(originalStartSegmentIndex, originalStartPointIndex, originalEndPoint.SegmentIndex, originalEndPoint.PointIndex).Select(point => point.Point),
                    SectionIndex = originalStartSegmentIndex,
                    Length = originalValues.Distance,
                    Diff = newValues - originalValues,
                });

                var exactNewPoints = EnumerateExactNewPoints(newStartSegmentIndex, newStartPointIndex, newEndPoint.SegmentIndex, newEndPoint.PointIndex).ToArray();

                (int SegmentIndex, int PointIndex) PreviousIndex(int segmentIndex, int pointIndex, int minSegmentIndex)
                {
                    if (--pointIndex < 0)
                    {
                        if (--segmentIndex < minSegmentIndex)
                        {
                            segmentIndex = minSegmentIndex;
                            pointIndex = 0;
                        }
                        else
                        {
                            // Indicates "Last point in segment"
                            pointIndex = -1;
                        }
                    }
                    return (segmentIndex, pointIndex);
                }
            }
        }

        (TrackDifference Difference, int Index)[] existingDifferences = Differences
            .Select((diff, index) => (diff, index))
            .Where(tuple => tuple.diff.SectionIndex >= originalStart && tuple.diff.SectionIndex <= originalEnd)
            .ToArray();

        int existingI = 0;
        if (differences.Count > 0)
        {
            int i = 0;
            while (i < Differences.Count && Differences[i].SectionIndex < originalStart)
            {
                i++;
            }
            for (int j = 0; j < differences.Count; j++)
            {
                while (existingI < existingDifferences.Length && existingDifferences[existingI].Index < i + j)
                {
                    existingI++;
                }
                if (existingI < existingDifferences.Length && existingDifferences[existingI].Index == i + j)
                {
                    Differences[i + j] = differences[j];
                    existingDifferences[existingI] = default;
                }
                else
                {
                    for (int restI = existingI; restI < existingDifferences.Length; restI++)
                    {
                        ref (TrackDifference Difference, int Index) existingDifference = ref existingDifferences[restI];
                        existingDifference.Index++;
                    }
                    Differences.Insert(i + j, differences[j]);
                }
            }
        }

        for (existingI = existingDifferences.Length - 1; existingI >= 0; existingI--)
        {
            var existingDifference = existingDifferences[existingI];
            if (existingDifference.Difference is not null)
            {
                StrongReferenceMessenger.Default.Send(new RemovingDifference(existingDifference));
                Differences.RemoveAt(existingDifference.Index);
            }
        }

        IEnumerable<IndexedPoint> EnumerateNewPoints(int startSegment, int endSegment)
        {
            for (int segmentIndex = startSegment; segmentIndex <= endSegment; segmentIndex++)
            {
                TrackPoint[] points = _newSegments[segmentIndex].Points;
                for (int pointIndex = 0; pointIndex < points.Length - 1; pointIndex++)
                {
                    yield return (points[pointIndex], segmentIndex, pointIndex);
                }
            }
            TrackPoint[] lastPoints = _newSegments[endSegment].Points;
            yield return (lastPoints[^1], endSegment, lastPoints.Length - 1);
        }

        IEnumerable<IndexedPoint> EnumerateExactNewPoints(int startSegment, int startPoint, int endSegment, int endPoint)
        {
            int startPointIndex = startPoint;
            for (int segmentIndex = startSegment; segmentIndex <= endSegment; segmentIndex++)
            {
                TrackPoint[] points = _newSegments[segmentIndex].Points;
                for (int pointIndex = (startPointIndex == -1 ? points.Length - 1 : startPointIndex); pointIndex <= (segmentIndex == endSegment ? (endPoint == -1 ? points.Length - 1 : endPoint) : points.Length - 2); pointIndex++)
                {
                    yield return (points[pointIndex], segmentIndex, pointIndex);
                }
                startPointIndex = 0;
            }
        }

        IEnumerable<IndexedPoint> EnumerateOriginalPoints(int startSegment, int startPoint, int endSegment, int endPoint)
        {
            int startPointIndex = startPoint;
            for (int segmentIndex = startSegment; segmentIndex <= endSegment; segmentIndex++)
            {
                TrackPoint[] points = _originalSegments[segmentIndex].Points;
                for (int pointIndex = (startPointIndex == -1 ? points.Length - 1 : startPointIndex); pointIndex <= (segmentIndex == endSegment ? (endPoint == -1 ? points.Length - 1 : endPoint) : points.Length - 2); pointIndex++)
                {
                    yield return (points[pointIndex], segmentIndex, pointIndex);
                }
                startPointIndex = 0;
            }
        }

        TrackPoint.CommonValues GetOriginalValues(TrackPoint startPoint, int startSegment, TrackPoint endPoint, int endSegment)
        {
            return _originalSegments
                .Skip(startSegment)
                .Take(endSegment - startSegment)
                .Aggregate(default(TrackPoint.CommonValues), (values, segment) => values + segment.Points.Last().Values) - startPoint.Values + endPoint.Values;
        }

        TrackPoint.CommonValues GetNewValues(TrackPoint startPoint, int startSegment, TrackPoint endPoint, int endSegment)
        {
            return _newSegments
                .Skip(startSegment)
                .Take(endSegment - startSegment)
                .Aggregate(default(TrackPoint.CommonValues), (values, segment) => values + segment.Points.Last().Values) - startPoint.Values + endPoint.Values;
        }
    }

    void IRecipient<SectionAdded>.Receive(SectionAdded message)
    {
        _newSegments.Insert(message.Index, new NewSegment { Start = message.Section.Start.Location });
    }

    void IRecipient<SectionRemoved>.Receive(SectionRemoved message)
    {
        _newSegments.RemoveAt(message.Index);
        // TODO Wenn der erste/letzte WayPoint entfernt wird, folgt keine Kalkulation aber die Diffs m�ssen trotzdem neu gefunden werden
    }

    void IRecipient<CalculationStarting>.Receive(CalculationStarting message)
    {
        _newSegments[_track.RouteBuilder.GetSectionIndex(message.Section)].Points = null;
    }

    void IRecipient<CalculationFinished>.Receive(CalculationFinished message)
    {
        if (!message.Section.IsCanceled && message.Result.IsValid)
        {
            int newIndex = _track.RouteBuilder.GetSectionIndex(message.Section);
            _newSegments[newIndex].Points = message.Result.Points;

            CreateDifferences(newIndex);

            OnPropertyChanged(nameof(Diff));
        }
    }
}
