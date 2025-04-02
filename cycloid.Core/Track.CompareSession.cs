using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using cycloid.Routing;

using IndexedPoint = (cycloid.TrackPoint Point, int SegmentIndex, int PointIndex);

namespace cycloid;

public class CompareSessionChanged(object sender, CompareSession oldValue, CompareSession newValue) : PropertyChangedMessage<CompareSession>(sender, null, oldValue, newValue);

public class TrackDifference
{
    public IEnumerable<TrackPoint> OriginalPoints { get; init; }
    public int SectionIndex { get; init; }
    public float Distance { get; init; }
    public float Length { get; init; }
    public TrackPoint.CommonValues Diff { get; init; }
    public bool IsNotSignificant { get; init; }
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

public partial class CompareSession : ObservableObject,
    IRecipient<SectionAdded>,
    IRecipient<SectionRemoved>,
    IRecipient<CalculationStarting>,
    IRecipient<CalculationFinished>
{
    private readonly struct OriginalSegment(float distance, TrackPoint[] points)
    {
        public float Distance => distance;
        public TrackPoint[] Points => points;
        public Dictionary<MapPoint, int> Indices { get; } = points.Select((point, index) => (Point: (MapPoint)point, Index: index)).ToLookup(tuple => tuple.Point).ToDictionary(lookup => lookup.Key, lookup => lookup.First().Index);
    }

    private class NewSegment
    {
        public MapPoint Start { get; init; }
        public TrackPoint[] Points { get; set; }
    }

    private class SignificanceComparer : IEqualityComparer<TrackPoint>
    {
        public static readonly SignificanceComparer Instance = new();

        private SignificanceComparer()
        { }

        public bool Equals(TrackPoint x, TrackPoint y)
        {
            return GeoCalculation.Distance(x, y) <= 1;
        }

        public int GetHashCode([DisallowNull] TrackPoint obj)
        {
            throw new NotImplementedException();
        }
    }

    private readonly Track _track;
    private readonly Profile _originalProfile;
    private readonly TrackPoint.CommonValues _originalValues;
    private readonly OriginalSegment[] _originalSegments;
    private readonly Dictionary<MapPoint, int> _originalSegmentIndices;
    private readonly WayPoint[] _originalWayPoints;
    private readonly List<NewSegment> _newSegments;

    public ObservableCollection<TrackDifference> Differences { get; } = [];

    [ObservableProperty]
    public partial TrackDifference? CurrentDifference { get; set; }

    public CompareSession(Track track, (WayPoint[] WayPoints, (float Distance, TrackPoint[] Points)[] TrackPoints) segments)
    {
        _track = track;
        _originalProfile = track.RouteBuilder.Profile;
        _originalValues = track.Points.Total;
        _originalSegments = segments.TrackPoints.Select(points => new OriginalSegment(points.Distance, points.Points)).ToArray();
        _originalSegmentIndices = segments.WayPoints.SkipLast(1).Select((point, index) => (Point: point, Index: index)).ToLookup(tuple => tuple.Point.Location).ToDictionary(lookup => lookup.Key, lookup => lookup.First().Index);
        _originalWayPoints = segments.WayPoints;
        _newSegments = segments.WayPoints.SkipLast(1).Zip(segments.TrackPoints, (wayPoint, points) => new NewSegment { Start = wayPoint.Location, Points = points.Points }).ToList();

        StrongReferenceMessenger.Default.Register<SectionAdded>(this);
        StrongReferenceMessenger.Default.Register<SectionRemoved>(this);
        StrongReferenceMessenger.Default.Register<CalculationStarting>(this);
        StrongReferenceMessenger.Default.Register<CalculationFinished>(this);
    }

    public CompareSession(Track track, Profile profile, TrackPoint.CommonValues values, WayPoint[] wayPoints, (float Distance, TrackPoint[] Points)[] trackPoints)
    {
        _track = track;
        _originalProfile = profile;
        _originalValues = values;
        _originalSegments = trackPoints.Select(segmentPoints => new OriginalSegment(segmentPoints.Distance, segmentPoints.Points)).ToArray();
        _originalSegmentIndices = wayPoints.SkipLast(1).Select((point, index) => (Point: point, Index: index)).ToLookup(tuple => tuple.Point.Location).ToDictionary(lookup => lookup.Key, lookup => lookup.First().Index);
        _originalWayPoints = wayPoints;
        _newSegments = [];
    }

    public void Initialize((WayPoint[] WayPoints, (float Distance, TrackPoint[] Points)[] TrackPoints) segments)
    {
        _newSegments.AddRange(segments.WayPoints.SkipLast(1).Zip(segments.TrackPoints, (wayPoint, points) => new NewSegment { Start = wayPoint.Location, Points = points.Points }));

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

    public IEnumerable<(float Distance, TrackPoint[] Points)> OriginalTrackPoints => _originalSegments.Select(segment => (segment.Distance, segment.Points));

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

        (TrackDifference Difference, int Index)[] removeDifferences = Differences
            .Select((diff, index) => (diff, index))
            .Where(tuple => tuple.diff.SectionIndex >= originalStart && tuple.diff.SectionIndex <= originalEnd)
            .ToArray();

        TrackDifference? currentDifference = null;

        for (int i = removeDifferences.Length - 1; i >= 0; i--)
        {
            (TrackDifference Difference, int Index) delete = removeDifferences[i];
            if (CurrentDifference is TrackDifference difference && difference == delete.Difference)
            {
                currentDifference = CurrentDifference;
                CurrentDifference = null;
            }
            Differences.RemoveAt(delete.Index);
        }

        IEnumerator<IndexedPoint> originalEnumerator = EnumerateOriginalPoints(originalStart, 0, originalEnd, -1).GetEnumerator();
        bool hasMore = originalEnumerator.MoveNext();
        IndexedPoint original = originalEnumerator.Current;

        bool differs = false;
        List<TrackDifference> addDifferences = [];

        IEnumerator<IndexedPoint> newEnumerator = EnumerateNewPoints(newStart, newEnd).GetEnumerator();
        IndexedPoint newPoint = default;
        IndexedPoint newDiffPoint = default;
        while (true)
        {
            if (!newEnumerator.MoveNext())
            {
                if (differs)
                {
                    while (originalEnumerator.MoveNext())
                    { }
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

                IEnumerable<TrackPoint> differentOriginalPoints = EnumerateOriginalPoints(original.SegmentIndex, original.PointIndex, originalEndPoint.SegmentIndex, originalEndPoint.PointIndex).Select(point => point.Point);
                IEnumerable<TrackPoint> differentNewPoints = EnumerateTrackPoints(newDiffPoint.SegmentIndex, newDiffPoint.PointIndex, newEndPoint.SegmentIndex, newEndPoint.PointIndex);
                if (differentNewPoints.SkipLast(1).SequenceEqual(differentOriginalPoints.SkipLast(1), SignificanceComparer.Instance))
                {
                    return;
                }

                addDifferences.Add(new TrackDifference
                {
                    OriginalPoints = EnumerateOriginalPoints(originalStartSegmentIndex, originalStartPointIndex, originalEndPoint.SegmentIndex, originalEndPoint.PointIndex).Select(point => point.Point),
                    SectionIndex = originalStartSegmentIndex,
                    Distance = _originalSegments[originalStartSegmentIndex].Distance + originalStartPoint.Distance,
                    Length = originalValues.Distance,
                    Diff = newValues - originalValues,
                });

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

        if (addDifferences.Count > 0)
        {
            int i = 0;
            while (i < Differences.Count && Differences[i].SectionIndex < originalStart)
            {
                i++;
            }
            for (int j = 0; j < addDifferences.Count; j++)
            {
                TrackDifference add = addDifferences[j];
                Differences.Insert(i + j, add);
                if (currentDifference is not null && add.Distance + add.Length >= currentDifference.Distance)
                {
                    CurrentDifference = add;
                    currentDifference = null;
                }
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

        IEnumerable<TrackPoint> EnumerateTrackPoints(int startSegment, int startPoint, int endSegment, int endPoint)
        {
            int startPointIndex = startPoint;
            for (int segmentIndex = startSegment; segmentIndex <= endSegment; segmentIndex++)
            {
                TrackPoint[] points = _newSegments[segmentIndex].Points;
                for (int pointIndex = (startPointIndex == -1 ? points.Length - 1 : startPointIndex); pointIndex <= (segmentIndex == endSegment ? (endPoint == -1 ? points.Length - 1 : endPoint) : points.Length - 2); pointIndex++)
                {
                    yield return points[pointIndex];
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
            return endPoint.Values - startPoint.Values + _originalSegments
                .Skip(startSegment)
                .Take(endSegment - startSegment)
                .Aggregate(default(TrackPoint.CommonValues), (values, segment) => values + segment.Points.Last().Values);
        }

        TrackPoint.CommonValues GetNewValues(TrackPoint startPoint, int startSegment, TrackPoint endPoint, int endSegment)
        {
            return endPoint.Values - startPoint.Values + _newSegments
                .Skip(startSegment)
                .Take(endSegment - startSegment)
                .Aggregate(default(TrackPoint.CommonValues), (values, segment) => values + segment.Points.Last().Values);
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
