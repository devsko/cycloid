using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.VisualStudio.Threading;

namespace cycloid.Routing;

public class RouteBuilder
{
    private static TaskCompletionSource<bool> CompletedTaskCompletionSource()
    {
        TaskCompletionSource<bool> tcs = new();
        tcs.SetResult(true);

        return tcs;
    }

    private readonly Dictionary<MapPoint, RouteSection> _sections = [];
    private TaskCompletionSource<bool> _volatileTaskSource = CompletedTaskCompletionSource();

    public ObservableCollection<MapPoint> Points { get; } = [];
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

    public IEnumerable<RouteSection> Sections 
        => Points.SkipLast(1).Select(point => _sections[point]);

    private void StartCalculation(RouteSection section)
    {
        _ = CalculateAsync();

        async Task CalculateAsync()
        {
            CalculationFinished?.Invoke(section, await GetResultAsync());
        }

        async Task<RouteResult> GetResultAsync()
        {
            SynchronizationContext capturedContext = SynchronizationContext.Current;

            CancellationToken cancellationToken = section.Cancellation.Token;
            try
            {
                if (section.DirectDistance > 50_000)
                {
                    await _volatileTaskSource.Task.WithCancellation(cancellationToken);
                }

                CalculationStarting?.Invoke(section);

                await TaskScheduler.Default;

                GeoJSON.Text.Feature.Feature feature = await Client.GetRouteAsync(section.Start, section.End, NoGoAreas, Profile, ClientRetry, cancellationToken).ConfigureAwait(false);

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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { }

            return default;

            void ClientRetry()
            {
                _ = RetryAsync();

                async Task RetryAsync()
                {
                    await capturedContext;
                    CalculationRetry?.Invoke(section);
                }
            }
        }
    }

    public void InsertPoint(MapPoint point, RouteSection section) 
        => AddPoint(point, Points.IndexOf(section.Start) + 1);

    public void AddLastPoint(MapPoint point) 
        => AddPoint(point, Points.Count);

    public void AddFirstPoint(MapPoint point) 
        => AddPoint(point, 0);

    private void AddPoint(MapPoint point, int index)
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

    public void RemovePoint(MapPoint point)
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

    public void MovePoint(MapPoint moveFrom, MapPoint moveTo)
    {
        int index = Points.IndexOf(moveFrom);
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
    }

    private void CreateSection(int startIndex, MapPoint? startPoint = null)
    {
        MapPoint point = startPoint ?? Points[startIndex];
        RouteSection section = new(point, Points[startIndex + 1]);
        _sections.Add(point, section);
        SectionAdded?.Invoke(section, startIndex);

        StartCalculation(section);
    }

    private void RemoveSection(int startIndex, MapPoint? startPoint = null)
    {
        MapPoint point = startPoint ?? Points[startIndex];
        if (!_sections.Remove(point, out RouteSection section))
        {
            throw new InvalidOperationException();
        }
        section.Cancellation.Cancel();
        SectionRemoved?.Invoke(section, startIndex);
    }

    public void StartVolatileTask()
    {
        EndVolatileTask();
        _volatileTaskSource = new TaskCompletionSource<bool>();
    }

    public void EndVolatileTask()
    {
        _volatileTaskSource.TrySetResult(true);
    }
}