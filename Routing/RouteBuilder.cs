using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
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

public partial class RouteBuilder
{
    private readonly Dictionary<WayPoint, RouteSection> _sections;
    private TaskCompletionSource<bool> _delayCalculationTaskSource;

    public ChangeLocker ChangeLock { get; }
    public ObservableCollection<WayPoint> Points { get; }
    public ObservableCollection<NoGoArea> NoGoAreas { get; }
    public BrouterClient Client { get; }
    public Profile Profile { get; set; }

    public RouteBuilder()
    {
        _sections = [];
        _delayCalculationTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Points = [];
        NoGoAreas = [];
        Client = new BrouterClient();
        Profile = new Profile
        {
            DownhillCost = Profile.DefaultDownhillCost,
            DownhillCutoff = Profile.DefaultDownhillCutoff,
            UphillCost = Profile.DefaultUphillCost,
            UphillCutoff = Profile.DefaultUphillCutoff,
            BikerPower = Profile.DefaultBikerPower,
        };
        ChangeLock = new ChangeLocker();
        DelayCalculation = false;
    }

    public IEnumerable<RouteSection> Sections 
        => Points.SkipLast(1).Select(point => _sections[point]);

    public bool DelayCalculation
    {
        get => !_delayCalculationTaskSource.Task.IsCompleted;
        set
        {
            if (value != DelayCalculation)
            {
                _delayCalculationTaskSource.TrySetResult(true);
                if (value)
                {
                    _delayCalculationTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
        }
    }

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
        DelayCalculation = false;
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
                if (section.DirectDistance > 25_000)
                {
                    StrongReferenceMessenger.Default.Send(new CalculationDelayed(section));

                    await _delayCalculationTaskSource.Task.WithCancellation(cancellationToken);
                }

                StrongReferenceMessenger.Default.Send(new CalculationStarting(section));

                await TaskScheduler.Default;

                if (section.IsDirectRoute)
                {
                    GeoJSON.Text.Geometry.IPosition startPosition = await Client.GetPositionAsync(section.Start.Location, Profile, RetryCallback, cancellationToken);
                    GeoJSON.Text.Geometry.IPosition endPosition = await Client.GetPositionAsync(section.End.Location, Profile, RetryCallback, cancellationToken);

                    if (startPosition is not null && endPosition is not null)
                    {
                        return TrackPointConverter.Convert(
                            RoutePoint.FromPosition(startPosition, TimeSpan.Zero), 
                            RoutePoint.FromPosition(endPosition, TimeSpan.FromHours(section.DirectDistance / 1_000 / 20)));
                    }
                }
                else
                {
                    GeoJSON.Text.Feature.Feature feature = await Client.GetRouteAsync(section.Start.Location, section.End.Location, NoGoAreas, Profile, RetryCallback, cancellationToken).ConfigureAwait(false);

                    if (feature is not null)
                    {
                        IEnumerable<float> times = ((JsonElement)feature.Properties["times"]).EnumerateArray().Select(e => e.GetSingle());
                        ReadOnlyCollection<GeoJSON.Text.Geometry.IPosition> positions = ((GeoJSON.Text.Geometry.LineString)feature.Geometry).Coordinates;

                        IEnumerable<RoutePoint> points = positions.Zip(times,
                            (position, time) => new RoutePoint(
                                (float)position.Latitude,
                                (float)position.Longitude,
                                (float)(position.Altitude ?? 0),
                                TimeSpan.FromSeconds(time)));

                        long GetProperty(string name) => long.Parse(((JsonElement)feature.Properties[name]).GetString()!);

                        long length = GetProperty("track-length");
                        long duration = GetProperty("total-time");
                        //long ascend = GetProperty("filtered ascend");

                        cancellationToken.ThrowIfCancellationRequested();

                        return TrackPointConverter.Convert(points, positions.Count);
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

    public async Task InsertPointsAsync(IEnumerable<WayPoint> wayPoints, WayPoint insertAfter)
    {
        using (await ChangeLock.EnterCalculationAsync())
        {
            int index = insertAfter is null ? 0 : Points.IndexOf(insertAfter) + 1;
            foreach (WayPoint wayPoint in wayPoints)
            {
                AddPoint(wayPoint, index++);
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
                            RoutePoint.FromMapPoint(point.Location, 0, TimeSpan.Zero),
                            RoutePoint.FromMapPoint(wayPoint.Location, 0, TimeSpan.Zero))
                        : TrackPointConverter.Convert(routePoints, routePoints.Length);

                    StrongReferenceMessenger.Default.Send(new CalculationStarting(section));
                    StrongReferenceMessenger.Default.Send(new CalculationFinished(section, result));
                }
            }
        }

        StrongReferenceMessenger.Default.Send(new RouteChanged(wasEmpty));
    }
}
