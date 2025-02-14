using CommunityToolkit.WinUI;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace cycloid.Controls;

public partial class MapLocationMenuFlyoutItem : MenuFlyoutItem
{
    public MapLocationMenuFlyoutItem()
    {
        DefaultStyleKey = typeof(MapLocationMenuFlyoutItem);
        IsEnabled = false;
    }
}

public partial class MapMenuFlyout : MenuFlyout
{
    private readonly MenuFlyoutItem _coordinates = new();
    private readonly MapLocationMenuFlyoutItem _address = new();

    [GeneratedDependencyProperty]
    public partial MapPoint Location { get; set; }

    public MapMenuFlyout()
    {
        Location = MapPoint.Invalid;
    }

    public void ShowAt(Map map, MapPoint location, Point position)
    {
        Location = location;
        _coordinates.Text = $"{Format.Latitude(location.Latitude)} {Format.Longitude(location.Longitude)}";
        _coordinates.Command = map.ViewModel.OpenLocationCommand;
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
            ShowAt(map, new FlyoutShowOptions { Position = position });
        }
    }

    protected override Control CreatePresenter()
    {
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