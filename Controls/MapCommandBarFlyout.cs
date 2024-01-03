using cycloid.Routing;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Markup;
using CommandBarFlyout = Microsoft.UI.Xaml.Controls.CommandBarFlyout;


namespace cycloid.Controls;

[ContentProperty(Name = nameof(Commands))]
public class MapCommandBarFlyout : CommandBarFlyout
{
    private readonly TextBlock _coordinates = new();
    private readonly TextBlock _address = new();

    public MapCommandBarFlyout()
    {
        Placement = FlyoutPlacementMode.Full;
    }

    public void ShowAt(FrameworkElement placementTarget, MapPoint point, Point position)
    {
        _coordinates.Text = $"{Format.Latitude(point.Latitude)} {Format.Longitude(point.Longitude)}";
        ShowAsync().FireAndForget();

        async Task ShowAsync()
        {
            string address = null;
            try
            {
                address = await ViewModel.GetAddressAsync(new Geopoint((BasicGeoposition)point));
            }
            catch
            { }
            _address.Text = address ?? string.Empty;
            ShowAt(placementTarget, new FlyoutShowOptions { Position = position });
        }
    }

    public IObservableVector<ICommandBarElement> Commands => SecondaryCommands;

    protected override Control CreatePresenter()
    {
        if (Commands.Count > 0)
        {
            Commands.Add(new AppBarSeparator());
        }

        Commands.Add(new AppBarElementContainer
        {
            Padding = new Thickness(10, 5, 10, 5),
            Content = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Children =
                {
                    _coordinates,
                    _address,
                },
            }
        });

        return base.CreatePresenter();
    }
}