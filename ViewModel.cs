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
    private TrackPoint? _currentPoint;

    [ObservableProperty]
    private TrackPoint? _hoverPoint;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrackHoverPointValuesEnabled))]
    private bool _mapHoverPointValuesEnabled;

    [ObservableProperty]
    private bool _trackVisible = true;

    [ObservableProperty]
    private bool _poisVisible = true;

    public event Action<Track, Track> TrackChanged;

    public ViewModel()
    {
        _synchronizationContext = SynchronizationContext.Current;

        if (_synchronizationContext is null)
        {
            throw new InvalidOperationException();
        }
    }

    public bool TrackHoverPointValuesEnabled
    {
        get => !MapHoverPointValuesEnabled;
        set => MapHoverPointValuesEnabled = !value;
    }

    partial void OnTrackChanged(Track oldValue, Track newValue)
    {
        CurrentPoint = null;
        HoverPoint = null;

        TrackChanged?.Invoke(oldValue, newValue);
    }
}