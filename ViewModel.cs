using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualStudio.Threading;

namespace cycloid;

public partial class ViewModel : ObservableObject
{
    private readonly SynchronizationContext _synchronizationContext;

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

    public readonly string OsmUri = "https://tile.openstreetmap.org/{zoomlevel}/{x}/{y}.png";

    [RelayCommand]
    public async Task OpenTrackAsync()
    {
        await Task.Yield();
    }

    [RelayCommand]
    public async Task NewTrackAsync()
    {
        await Task.Yield();
    }

    public async Task SetCurrentPointAsync(TrackPoint point)
    {
        await _synchronizationContext;
        CurrentPoint = point;
    }
}