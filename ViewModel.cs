using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Devices.Geolocation;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace cycloid;

public readonly record struct TrackPoint(float Latitude, float Longitude, float Altitude, float Gradient, float Heading)
{
    public static implicit operator BasicGeoposition(TrackPoint point) => new()
    { 
        Latitude = point.Latitude, 
        Longitude = point.Longitude, 
    };
}

[ObservableObject]
public partial class ViewModel : DependencyObject
{
    [ObservableProperty]
    private bool _heatmapVisible;

    [ObservableProperty]
    private Track _track = new();

    [ObservableProperty]
    private TrackPoint? _currentPoint = new() { Latitude = 46.46039124618558f, Longitude = 10.089039490153148f, Gradient = 5f, Heading = 195 };

    public async Task SetCurrentPointAsync(TrackPoint point)
    {
        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => CurrentPoint = point).AsTask().ConfigureAwait(false);
    }
}