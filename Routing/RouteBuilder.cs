using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace cycloid.Routing;

public class RouteBuilder
{
    private readonly Dictionary<WayPoint, RouteSection> _sections = [];
    private TaskCompletionSource<bool> _delayCalculationTaskSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ObservableCollection<WayPoint> Points { get; } = [];
    public ObservableCollection<NoGoArea> NoGoAreas { get; } = [];
    public BrouterClient Client { get; } = new();
    public Profile Profile { get; set; } = new()
    {
        DownhillCost = 80,
        DownhillCutoff = 0.5f,
        UphillCost = 100,
        UphillCutoff = 3.6f,
        BikerPower = 170,
    };

    public event Action<RouteSection, int> SectionAdded;
    public event Action<RouteSection, int> SectionRemoved;

    public event Action<RouteSection> CalculationStarting;
    public event Action<RouteSection> CalculationRetry;
    public event Action<RouteSection, RouteResult> CalculationFinished;

    public RouteBuilder()
    {
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

    public void StartCalculation(RouteSection section)
    {
        CalculateAsync().FireAndForget();

        async Task CalculateAsync()
        {
            CalculationFinished?.Invoke(section, await GetResultAsync());
        }

        async Task<RouteResult> GetResultAsync()
        {
            SynchronizationContext capturedContext = SynchronizationContext.Current;

            section.Cancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = section.Cancellation.Token;
            try
            {
                if (section.DirectDistance > 25_000)
                {
                    await _delayCalculationTaskSource.Task.WithCancellation(cancellationToken);
                }

                CalculationStarting?.Invoke(section);

                await TaskScheduler.Default;

                if (section.IsDirectRoute)
                {
                    GeoJSON.Text.Geometry.IPosition startPosition = await Client.GetPositionAsync(section.Start.Location, Profile, RetryCallback, cancellationToken);
                    GeoJSON.Text.Geometry.IPosition endPosition = await Client.GetPositionAsync(section.End.Location, Profile, RetryCallback, cancellationToken);

                    if (startPosition is not null && endPosition is not null)
                    {
                        TimeSpan duration = TimeSpan.FromHours(section.DirectDistance / 1_000 / 20);
                        return new RouteResult(
                        [
                            new RoutePoint((float)startPosition.Latitude, (float)startPosition.Longitude, (float)(startPosition.Altitude ?? 0), TimeSpan.Zero),
                            new RoutePoint((float)endPosition.Latitude, (float)endPosition.Longitude, (float)(endPosition.Altitude ?? 0), duration)
                        ], 2, (long)section.DirectDistance, (long)duration.TotalSeconds);
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

                        return new RouteResult(points, positions.Count, length, duration);
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
                    CalculationRetry?.Invoke(section);
                }
            }
        }
    }

    public WayPoint InsertPoint(MapPoint location, RouteSection section)
    {
        WayPoint wayPoint = new(location, section.IsDirectRoute);
        AddPoint(wayPoint, Points.IndexOf(section.Start) + 1);

        return wayPoint;
    }

    public void AddLastPoint(WayPoint wayPoint) => AddPoint(wayPoint, Points.Count);

    public void AddFirstPoint(WayPoint wayPoint) => AddPoint(wayPoint, 0);

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

    public void RemovePoint(WayPoint point)
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

    public WayPoint MovePoint(WayPoint moveFrom, MapPoint location)
    {
        int index = Points.IndexOf(moveFrom);
        WayPoint moveTo = new(location, moveFrom.IsDirectRoute);
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

    private void CreateSection(int startIndex, WayPoint startPoint = null)
    {
        WayPoint point = startPoint ?? Points[startIndex];
        RouteSection section = new(point, Points[startIndex + 1]);
        _sections.Add(point, section);
        SectionAdded?.Invoke(section, startIndex);

        StartCalculation(section);
    }

    private void RemoveSection(int startIndex, WayPoint startPoint = null)
    {
        WayPoint point = startPoint ?? Points[startIndex];
        if (!_sections.Remove(point, out RouteSection section))
        {
            throw new InvalidOperationException();
        }
        section.Cancellation.Cancel();
        SectionRemoved?.Invoke(section, startIndex);
    }
}
