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

    public ViewModel()
    {
        _synchronizationContext = SynchronizationContext.Current;
    }


    [ObservableProperty]
    private Track _track = new();

    [ObservableProperty]
    private TrackPoint? _currentPoint;

    public string OsmUri => "https://tile.openstreetmap.org/{zoomlevel}/{x}/{y}.png";

    [RelayCommand]
    public async Task OpenTrackAsync()
    {

    }

    [RelayCommand]
    public async Task NewTrackAsync()
    {

    }

    public async Task SetCurrentPointAsync(TrackPoint point)
    {
        await _synchronizationContext;
        CurrentPoint = point;
    }
}