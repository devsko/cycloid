using System;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using cycloid.External;

namespace cycloid;

public partial class ViewModel : ObservableObject
{
    private readonly SynchronizationContext _synchronizationContext;

    public Strava Strava { get; } = new();

    public Osm Osm { get; } = new();

    [ObservableProperty]
    private Track _track;

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
            oldValue.RouteBuilder.Changed -= RouteBuilder_Changed;
        }
        if (newValue is not null)
        {
            newValue.RouteBuilder.Changed += RouteBuilder_Changed;
        }

        TrackChanged?.Invoke(oldValue, newValue);
    }

    private void RouteBuilder_Changed()
    {
        if (!IsCaptured)
        {
            SaveTrackAsync().FireAndForget();
        }
    }
}