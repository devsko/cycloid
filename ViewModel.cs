using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualStudio.Threading;
using Windows.Devices.Geolocation;

namespace cycloid;

public readonly record struct TrackPoint(float Latitude, float Longitude, float Altitude, float Gradient, float Heading)
{
    public static implicit operator BasicGeoposition(TrackPoint point) => new()
    { 
        Latitude = point.Latitude, 
        Longitude = point.Longitude, 
    };
}

public partial class ViewModel : ObservableObject
{
    private readonly SynchronizationContext _synchronizationContext;

    public ViewModel()
    {
        _synchronizationContext = SynchronizationContext.Current;
    }

    private bool _heatmapVisible;
    public bool HeatmapVisible 
    {
        get => _heatmapVisible;
        private set => SetProperty(ref _heatmapVisible, value);
    }

    public string HeatmapUri => App.Current.Strava.HeatmapUri;

    [ObservableProperty]
    private Track _track = new();

    [ObservableProperty]
    private TrackPoint? _currentPoint;

    [RelayCommand]
    public async Task ToggleHeatmapVisibleAsync()
    {
        if (HeatmapVisible)
        {
            HeatmapVisible = false;
        }
        else
        {
            HeatmapVisible = await App.Current.Strava.InitializeHeatmapAsync();
            // Notify property changed again to convinvce the toggle button
            OnPropertyChanged(nameof(HeatmapVisible));
        }
    }

    public async Task SetCurrentPointAsync(TrackPoint point)
    {
        await _synchronizationContext;
        CurrentPoint = point;
    }
}