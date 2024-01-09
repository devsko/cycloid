using System;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using cycloid.External;
using cycloid.Routing;

namespace cycloid;

public partial class ViewModel : ObservableObject
{
    private readonly SynchronizationContext _synchronizationContext;

    public Strava Strava { get; } = new();

    public Osm Osm { get; } = new();

    [ObservableProperty]
    private Track _track;

    [ObservableProperty]
    private bool _trackIsCalculating;

    [ObservableProperty]
    private TrackPoint _currentPoint = TrackPoint.Invalid;

    [ObservableProperty]
    private TrackPoint _hoverPoint = TrackPoint.Invalid;

    [ObservableProperty]
    private string _status;

    public event Action<Track, Track> TrackChanged;

    public ViewModel()
    {
        _synchronizationContext = SynchronizationContext.Current;

        if (_synchronizationContext is null)
        {
            throw new InvalidOperationException();
        }
    }

    public bool ProfileHoverPointValuesEnabled
    {
        get => !MapHoverPointValuesEnabled;
        set => MapHoverPointValuesEnabled = !value;
    }

    partial void OnTrackChanged(Track oldValue, Track newValue)
    {
        CurrentPoint = TrackPoint.Invalid;
        HoverPoint = TrackPoint.Invalid;

        if (oldValue is not null)
        {
            oldValue.RouteBuilder.CalculationStarting -= RouteBuilder_CalculationStarting;
            oldValue.RouteBuilder.CalculationFinished -= RouteBuilder_CalculationFinished;
            oldValue.RouteBuilder.Changed -= RouteBuilder_Changed;
            oldValue.RouteBuilder.FileSplitChanged -= RouteBuilder_FileSplitChanged;
        }
        if (newValue is not null)
        {
            newValue.RouteBuilder.CalculationStarting += RouteBuilder_CalculationStarting;
            newValue.RouteBuilder.CalculationFinished += RouteBuilder_CalculationFinished;
            newValue.RouteBuilder.Changed += RouteBuilder_Changed;
            newValue.RouteBuilder.FileSplitChanged += RouteBuilder_FileSplitChanged;
        }

        TrackChanged?.Invoke(oldValue, newValue);
    }

    private void RouteBuilder_CalculationStarting(RouteSection _)
    {
        TrackIsCalculating = true;
    }

    private void RouteBuilder_CalculationFinished(RouteSection _1, RouteResult _2)
    {
        TrackIsCalculating = false;
    }

    private void RouteBuilder_Changed()
    {
        if (!IsCaptured)
        {
            SaveTrackAsync().FireAndForget();
        }
    }

    private void RouteBuilder_FileSplitChanged(WayPoint wayPoint)
    {
        SaveTrackAsync().FireAndForget();
    }
}