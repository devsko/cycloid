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

    public MapPoint Location
    {
        get => (MapPoint)GetValue(LocationProperty);
        set => SetValue(LocationProperty, value);
    }

    public static readonly DependencyProperty LocationProperty =
        DependencyProperty.Register(nameof(Location), typeof(MapPoint), typeof(MapCommandBarFlyout), new PropertyMetadata(MapPoint.Invalid));

    public MapCommandBarFlyout()
    {
        Placement = FlyoutPlacementMode.Full;
    }

    public void ShowAt(FrameworkElement placementTarget, MapPoint location, Point position)
    {
        Location = location;
        _coordinates.Text = $"{Format.Latitude(location.Latitude)} {Format.Longitude(location.Longitude)}";
        ShowAsync().FireAndForget();

        async Task ShowAsync()
        {
            string address = null;
            try
            {
                address = await ViewModel.GetAddressAsync(new Geopoint((BasicGeoposition)location));
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