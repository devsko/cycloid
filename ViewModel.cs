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
    [NotifyCanExecuteChangedFor(nameof(RecalculateCommand))]
    private Track _track;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecalculateCommand))]
    private bool _trackIsInitialized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrackIsCalculating))]
    [NotifyPropertyChangedFor(nameof(CompareSessionState))]
    private int _trackCalculationCounter;

    [ObservableProperty]
    private TrackPoint _currentPoint = TrackPoint.Invalid;

    [ObservableProperty]
    private TrackPoint _hoverPoint = TrackPoint.Invalid;

    [ObservableProperty]
    private string _status;

    public event Action<Track, Track> TrackChanged;
    public event Action<TrackPoint, TrackPoint> HoverPointChanged;

    public ViewModel()
    {
        _synchronizationContext = SynchronizationContext.Current;

        if (_synchronizationContext is null)
        {
            throw new InvalidOperationException();
        }
    }

    public bool TrackIsCalculating => TrackCalculationCounter > 0;

    public bool ProfileHoverPointValuesEnabled
    {
        get => !MapHoverPointValuesEnabled;
        set => MapHoverPointValuesEnabled = !value;
    }

    partial void OnTrackChanged(Track oldValue, Track newValue)
    {
        TrackIsInitialized = false;
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

    partial void OnHoverPointChanged(TrackPoint oldValue, TrackPoint newValue)
    {
        HoverPointChanged?.Invoke(oldValue, newValue);
    }

    private void RouteBuilder_CalculationStarting(RouteSection _)
    {
        TrackCalculationCounter++;
    }

    private void RouteBuilder_CalculationFinished(RouteSection _1, RouteResult _2)
    {
        TrackCalculationCounter--;
    }

    private void RouteBuilder_Changed(bool initialization)
    {
        if (!initialization && !IsCaptured)
        {
            SaveTrackAsync().FireAndForget();
        }
    }

    private void RouteBuilder_FileSplitChanged(WayPoint wayPoint)
    {
        SaveTrackAsync().FireAndForget();
    }
}