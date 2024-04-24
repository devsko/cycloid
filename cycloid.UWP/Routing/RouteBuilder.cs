using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.VisualStudio.Threading;

namespace cycloid.Routing;

public abstract class SectionIndexMessage(RouteSection section, int index)
{
    public RouteSection Section => section;
    public int Index => index;
}

public class SectionAdded(RouteSection section, int index) : SectionIndexMessage(section, index);
public class SectionRemoved(RouteSection section, int index) : SectionIndexMessage(section, index);

public abstract class CalculationMessage(RouteSection section)
{
    public RouteSection Section => section;
}

public class CalculationStarting(RouteSection section) : CalculationMessage(section);
public class CalculationDelayed(RouteSection section) : CalculationMessage(section);
public class CalculationRetry(RouteSection section) : CalculationMessage(section);
public class CalculationFinished(RouteSection section, RouteResult result) : CalculationMessage(section)
{
    public RouteResult Result => result;
}

public class RouteChanging();

public class RouteChanged(bool initialization)
{
    public bool Initialization => initialization;
}

public class FileSplitChanged(WayPoint wayPoint)
{
    public WayPoint WayPoint => wayPoint;
}

public enum CalculationDelayMode
{
    None,
    LongSections,
    Always,
}

public partial class RouteBuilder
{
    private readonly Dictionary<WayPoint, RouteSection> _sections;
    private TaskCompletionSource<bool> _delayTaskSource;
    private CalculationDelayMode _delayMode;

    public ChangeLocker ChangeLock { get; }
    public ObservableCollection<WayPoint> Points { get; }
    public ObservableCollection<NoGoArea> NoGoAreas { get; }
    public BrouterClient Client { get; }
    public Profile Profile { get; set; }

    public RouteBuilder()
    {
        _sections = [];
        _delayTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Points = [];
        NoGoAreas = [];
        Client = new BrouterClient();
        Profile = new Profile();
        ChangeLock = new ChangeLocker();
        DelayCalculation = CalculationDelayMode.None;
    }

    public IEnumerable<RouteSection> Sections 
        => Points.SkipLast(1).Select(point => _sections[point]);

    public CalculationDelayMode DelayCalculation
    {
        get => _delayMode;
        set
        {
            bool shouldDelay = value != CalculationDelayMode.None;
            bool isDelayed = !_delayTaskSource.Task.IsCompleted;
            if (shouldDelay != isDelayed)
            {
                _delayTaskSource.TrySetResult(true);
                if (shouldDelay)
                {
                    _delayTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
            _delayMode = value;
        }
    }

    public bool IsCalculating => ChangeLock.RunningCalculationCounter > 0;

    public void SetIsDirectRoute(RouteSection section, bool value)
    {
        section.IsDirectRoute = value;
        StartCalculation(section);
    }

    public void SetFileSplit(WayPoint wayPoint, bool value)
    {
        wayPoint.IsFileSplit = value;

        StrongReferenceMessenger.Default.Send(new FileSplitChanged(wayPoint));
    }

    public (RouteSection To, RouteSection From) GetSections(WayPoint point)
    {
        int index = Points.IndexOf(point);

        RouteSection to = index == 0 ? null : _sections[Points[index - 1]];
        RouteSection from = index == Points.Count - 1 ? null : _sections[point];

        return (to, from);
    }

    public void StartCalculation(RouteSection section)
    {
        CalculateAsync(section).FireAndForget();
    }

    public async Task RecalculateAllAsync(CancellationToken cancellationToken)
    {
        DelayCalculation = CalculationDelayMode.None;
        using (cancellationToken.Register(state => CancelAll((RouteBuilder)state), this, false))
        {
            await Task.WhenAll(_sections.Values.Select(CalculateAsync));
        }
        cancellationToken.ThrowIfCancellationRequested();

        static void CancelAll(RouteBuilder routeBuiler)
        {
            foreach (RouteSection section in routeBuiler._sections.Values)
            {
                section.Cancel();
            }
        }
    }

    private async Task CalculateAsync(RouteSection section)
    {
        section.ResetCancellation();
        using (await ChangeLock.EnterCalculationAsync(section.CancellationToken))
        {
            RouteResult result = await GetResultAsync(section.CancellationToken);

            StrongReferenceMessenger.Default.Send(new CalculationFinished(section, result));
        }

        async Task<RouteResult> GetResultAsync(CancellationToken cancellationToken)
        {
            SynchronizationContext capturedContext = SynchronizationContext.Current;

            try
            {
                if (_delayMode == CalculationDelayMode.Always || section.DirectDistance > 25_000)
                {
                    StrongReferenceMessenger.Default.Send(new CalculationDelayed(section));

                    await _delayTaskSource.Task.WithCancellation(cancellationToken);
                }

                StrongReferenceMessenger.Default.Send(new CalculationStarting(section));

                await TaskScheduler.Default;

                if (section.IsDirectRoute)
                {
                    RoutePoint? start = await Client.GetPositionAsync(section.Start.Location, Profile, RetryCallback, cancellationToken);
                    RoutePoint? end = await Client.GetPositionAsync(section.End.Location, Profile, RetryCallback, cancellationToken);

                    if (start is not null && end is not null)
                    {
                        return TrackPointConverter.Convert(start.Value, end.Value with { Time = TimeSpan.FromHours(section.DirectDistance / 1_000 / 20) });
                    }
                }
                else
                {
                    (IEnumerable<RoutePoint> points, int pointCount, IEnumerable<SurfacePart> surfaces) = await Client.GetRouteAsync(section.Start.Location, section.End.Location, NoGoAreas, Profile, RetryCallback, cancellationToken).ConfigureAwait(false);

                    if (points is not null)
                    { 
                        cancellationToken.ThrowIfCancellationRequested();

                        return TrackPointConverter.Convert(points, pointCount, surfaces);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { }

            return default;


            void RetryCallback()
            {
                RetryAsync().FireAndForget();

                async Task RetryAsync()
                {
                    await capturedContext;

                    StrongReferenceMessenger.Default.Send(new CalculationRetry(section));
                }
            }
        }
    }

    public async Task<WayPoint> InsertPointAsync(MapPoint location, RouteSection section)
    {
        using (await ChangeLock.EnterCalculationAsync())
        {
            WayPoint wayPoint = new(location, section.IsDirectRoute, false);
            AddPoint(wayPoint, Points.IndexOf(section.Start) + 1);

            return wayPoint;
        }
    }

    public async Task DeletePointsAsync(IEnumerable<WayPoint> wayPoints)
    {
        using (await ChangeLock.EnterCalculationAsync())
        {
            try
            {
                DelayCalculation = CalculationDelayMode.Always;
                foreach (WayPoint wayPoint in wayPoints)
                {
                    RemovePoint(wayPoint);
                }
            }
            finally
            {
                DelayCalculation = CalculationDelayMode.None;
            }
        }
    }

    public async Task InsertPointsAsync(IEnumerable<WayPoint> wayPoints, WayPoint insertAfter)
    {
        using (await ChangeLock.EnterCalculationAsync())
        {
            try
            {
                DelayCalculation = CalculationDelayMode.Always;
                int index = insertAfter is null ? 0 : Points.IndexOf(insertAfter) + 1;
                foreach (WayPoint wayPoint in wayPoints)
                {
                    AddPoint(wayPoint, index++);
                }
            }
            finally
            {
                DelayCalculation = CalculationDelayMode.None;
            }
        }
    }

    public Task AddLastPointAsync(WayPoint wayPoint) => AddPointAsync(wayPoint, Points.Count);

    public Task AddFirstPointAsync(WayPoint wayPoint) => AddPointAsync(wayPoint, 0);

    private async Task AddPointAsync(WayPoint point, int index)
    {
        using (await ChangeLock.EnterCalculationAsync())
        {
            AddPoint(point, index);
        }
    }

    private void AddPoint(WayPoint point, int index)
    {
        Points.Insert(index, point);
        if (Points.Count > 1)
        {
            if (index == Points.Count - 1)
            {
                CreateSection(index - 1);
            }
            else if (index == 0)
            {
                CreateSection(index);
            }
            else
            {
                RemoveSection(index - 1);
                CreateSection(index - 1);
                CreateSection(index);
            }
        }
    }

    public async Task RemovePointAsync(WayPoint point)
    {
        using (await ChangeLock.EnterCalculationAsync())
        {
            RemovePoint(point);
        }
    }

    private void RemovePoint(WayPoint point)
    {
        int index = Points.IndexOf(point);
        Points.RemoveAt(index);

        int removed = 0;
        if (index < Points.Count)
        {
            RemoveSection(index, point);
            removed++;
        }
        if (index > 0)
        {
            RemoveSection(index - 1);
            removed++;
        }
        if (removed == 2)
        {
            CreateSection(index - 1);
        }
    }

    public async Task<WayPoint> MovePointAsync(WayPoint moveFrom, MapPoint location)
    {
        using (await ChangeLock.EnterCalculationAsync())
        {
            int index = Points.IndexOf(moveFrom);
            WayPoint moveTo = new(location, moveFrom.IsDirectRoute, moveFrom.IsFileSplit);
            Points[index] = moveTo;

            if (index > 0)
            {
                RemoveSection(index - 1);
                CreateSection(index - 1);
            }
            if (index < Points.Count - 1)
            {
                RemoveSection(index, moveFrom);
                CreateSection(index, moveTo);
            }

            return moveTo;
        }
    }

    private void CreateSection(int startIndex, WayPoint startPoint = null)
    {
        WayPoint point = startPoint ?? Points[startIndex];
        RouteSection section = new(point, Points[startIndex + 1]);
        _sections.Add(point, section);

        StrongReferenceMessenger.Default.Send(new SectionAdded(section, startIndex));

        StartCalculation(section);
    }

    private void RemoveSection(int startIndex, WayPoint startPoint = null)
    {
        WayPoint point = startPoint ?? Points[startIndex];
        if (!_sections.Remove(point, out RouteSection section))
        {
            throw new InvalidOperationException();
        }
        section.Cancel();

        StrongReferenceMessenger.Default.Send(new SectionRemoved(section, startIndex));
    }

    public int GetSectionIndex(RouteSection section)
    {
        return Points.IndexOf(section.Start);
    }

    public async Task InitializeAsync(IEnumerable<(WayPoint WayPoint, RoutePoint[] RoutePoints)> data)
    {
        bool wasEmpty;
        using (await ChangeLock.EnterAsync(default))
        {
            wasEmpty = _sections.Count == 0;

            while (_sections.Count > 0)
            {
                RemoveSection(0);
                Points.RemoveAt(0);
            }
            if (Points.Count == 1)
            {
                Points.RemoveAt(0);
            }

            foreach ((WayPoint wayPoint, RoutePoint[] routePoints) in data)
            {
                Points.Add(wayPoint);
                if (Points.Count > 1)
                {
                    int startIndex = Points.Count - 2;
                    WayPoint point = Points[startIndex];
                    RouteSection section = new(point, wayPoint);
                    _sections.Add(point, section);

                    StrongReferenceMessenger.Default.Send(new SectionAdded(section, startIndex));

                    RouteResult result = routePoints is null
                        ? TrackPointConverter.Convert(
                            RoutePoint.FromMapPoint(point.Location, 0, TimeSpan.Zero, Surface.Unknown),
                            RoutePoint.FromMapPoint(wayPoint.Location, 0, TimeSpan.Zero, Surface.Unknown))
                        : TrackPointConverter.Convert(routePoints, routePoints.Length, null);

                    StrongReferenceMessenger.Default.Send(new CalculationStarting(section));
                    StrongReferenceMessenger.Default.Send(new CalculationFinished(section, result));
                }
            }
        }

        StrongReferenceMessenger.Default.Send(new RouteChanged(wasEmpty));
    }
}
