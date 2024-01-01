using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Services.Maps;

namespace cycloid;

partial class ViewModel
{
    private readonly Throttle<TrackPoint, ViewModel> _bingThrottle = new(GetAddressAsync, TimeSpan.FromSeconds(5));

    [ObservableProperty]
    private string _currentPointAddress;

    partial void OnCurrentPointChanged(TrackPoint value)
    {
        _bingThrottle.Next(value, this);
    }

    private static async Task GetAddressAsync(TrackPoint point, ViewModel @this)
    {
        if (!point.IsValid)
        {
            @this.CurrentPointAddress = string.Empty;
        }
        else
        {
            MapLocationFinderResult result = await MapLocationFinder.FindLocationsAtAsync(Convert.ToGeopoint(point));
            if (result.Status == MapLocationFinderStatus.Success && result.Locations is [MapLocation location, ..])
            {
                MapAddress address = location.Address;
                @this.CurrentPointAddress = $"{address.Town}, {address.District}, {address.Region}, {address.Country}";
            }
        }
    }
}