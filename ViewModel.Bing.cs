using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Services.Maps;

namespace cycloid;

partial class ViewModel
{
    private readonly AsyncThrottle<TrackPoint, ViewModel> _getAddressThrottle = new(GetAddressAsync, TimeSpan.FromSeconds(5));

    private string _currentPointAddress;
    public string CurrentPointAddress
    {
        get => _currentPointAddress;
        set => SetProperty(ref _currentPointAddress, value);
    }

    private static async Task GetAddressAsync(TrackPoint point, ViewModel @this, CancellationToken _)
    {
        if (!point.IsValid)
        {
            @this.CurrentPointAddress = string.Empty;
        }
        else
        {
            string address = await GetAddressAsync(Convert.ToGeopoint(point));
            if (address is not null)
            {
                @this.CurrentPointAddress = address;
            }
        }
    }

    public static async Task<string> GetAddressAsync(Geopoint point)
    {
        MapLocationFinderResult result = await MapLocationFinder.FindLocationsAtAsync(point);
        if (result.Status == MapLocationFinderStatus.Success && result.Locations is [MapLocation location, ..])
        {
            MapAddress address = location.Address;
            return $"{address.Town}, {address.District}, {address.Region}, {address.Country}";
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