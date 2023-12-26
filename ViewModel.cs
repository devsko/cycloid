using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Devices.Geolocation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

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
    private static readonly Geopoint _emptyGeopoint = new(new BasicGeoposition());
    
    private static readonly SolidColorBrush _positiveGradientBrush = new(Colors.Red);
    private static readonly SolidColorBrush _negativeGradientBrush = new(Colors.Green);

    public static string Gradient(float gradient) => $"{gradient:N1} %";

    public static Brush GradientToBrush(float gradient) => gradient >= 0 ? _positiveGradientBrush : _negativeGradientBrush;

    public static Geopoint ToGeopoint(TrackPoint? point) => point is TrackPoint p ? new Geopoint(p) : _emptyGeopoint;

    public static Visibility VisibleIfNotNull(object value) => value is null ? Visibility.Collapsed : Visibility.Visible;

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

    public void StartAnimation()
    {
        _ = Task.Run((Func<Task>)(async () =>
        {
            try
            {
                float latitude = 46.46039124618558f;
                float longitude = 10.089039490153148f;
                float grade = 5;
                for (int i = 0; i < 10000; i++)
                {
                    await SetCurrentPointAsync(new() { Latitude = latitude, Longitude = longitude, Gradient = grade }).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(1f / 60)).ConfigureAwait(false);

                    latitude += .0001f;
                    longitude += .0001f;
                    if (i % 30 == 0)
                    {
                        grade = -grade;
                    }
                }
            }
            catch
            { }
        }));
    }
}