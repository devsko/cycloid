using System.Threading.Tasks;
using CommunityToolkit.WinUI;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace cycloid.Controls;

public class MapMenuFlyout : MenuFlyout
{
    private readonly MenuFlyoutItem _coordinates = new();
    private readonly MenuFlyoutItem _address = new();
    private Style _locationItemStyle;

    public MapPoint Location
    {
        get => (MapPoint)GetValue(LocationProperty);
        set => SetValue(LocationProperty, value);
    }

    public static readonly DependencyProperty LocationProperty =
        DependencyProperty.Register(nameof(Location), typeof(MapPoint), typeof(MapMenuFlyout), new PropertyMetadata(MapPoint.Invalid));

    public void ShowAt(FrameworkElement placementTarget, MapPoint location, Point position)
    {
        _locationItemStyle ??= (Style)placementTarget.FindResource("LocationMenuItemStyle");

        Location = location;
        _coordinates.Text = $"{Format.Latitude(location.Latitude)} {Format.Longitude(location.Longitude)}";
        _coordinates.Command = ((ViewModel)placementTarget.FindResource(nameof(ViewModel))).OpenLocationCommand;
        _coordinates.CommandParameter = location;

        ShowAsync().FireAndForget();

        async Task ShowAsync()
        {
            string address = null;
            try
            {
                address = await ViewModel.GetAddressAsync(new Geopoint(location.ToBasicGeoposition()));
            }
            catch
            { }
            _address.Text = address ?? string.Empty;
            ShowAt(placementTarget, new FlyoutShowOptions { Position = position });
        }
    }

    protected override Control CreatePresenter()
    {
        _address.IsEnabled = false;
        _address.Style = _locationItemStyle;

        if (Items.Count > 0)
        {
            Items.Add(new MenuFlyoutSeparator());
        }
        Items.Add(_coordinates);
        Items.Add(_address);

        Control control = base.CreatePresenter();
        
        return control;
    }
}