using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Devices.Geolocation;
using Windows.Services.Maps;

namespace cycloid;

partial class ViewModel
{
    private readonly AsyncThrottle<TrackPoint, ViewModel> _getAddressThrottle = new(
        static (value, @this, cancellationToken) => @this.GetAddressAsync(value, cancellationToken),
        TimeSpan.FromSeconds(5));

    [ObservableProperty]
    public partial string CurrentPointAddress { get; set; }

    private async Task GetAddressAsync(TrackPoint point, CancellationToken _)
    {
        if (!point.IsValid)
        {
            CurrentPointAddress = string.Empty;
        }
        else
        {
            string address = await GetAddressAsync(Convert.ToGeopoint(point));
            if (address is not null)
            {
                CurrentPointAddress = address;
            }
        }
    }

    public static async Task<string> GetAddressAsync(Geopoint point, bool shorter = false)
    {
        MapLocationFinderResult result = await MapLocationFinder.FindLocationsAtAsync(point);
        if (result.Status == MapLocationFinderStatus.Success && result.Locations is [MapLocation location, ..])
        {
            MapAddress address = location.Address;
            return shorter
                ? $"{address.Town}, {address.CountryCode}"
                : $"{address.Town}, {address.District}, {address.Region}, {address.Country}";
        }

        return null;
    }

    public static async Task<Geopoint> GetLocationAsync(string address, Geopoint hint)
    {
        MapLocationFinderResult result = await MapLocationFinder.FindLocationsAsync(address, hint);
        if (result.Status == MapLocationFinderStatus.Success && result.Locations is [MapLocation location, ..])
        {
            return location.Point;
        }

        return null;
    }
}