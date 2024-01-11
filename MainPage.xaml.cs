using System;
using System.Collections.Generic;
using Windows.Devices.Geolocation;
using Windows.Services.Maps;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object _1, RoutedEventArgs _2)
    {
        Map.SetCenterAsync("Stampfl Samerberg", 8).FireAndForget();
        ViewModel.ToggleHeatmapVisibleAsync().FireAndForget();
        ViewModel.OpenLastTrackAsync().FireAndForget();
    }

    private void ViewModel_TrackChanged(Track _, Track track)
    {
        ApplicationView.GetForCurrentView().Title = track is null ? string.Empty : track.Name;
    }

    private async void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            MapLocationFinderResult result = await MapLocationFinder.FindLocationsAsync(sender.Text, new Geopoint(new BasicGeoposition { Latitude = 47.5, Longitude = 12 }));
            if (result.Status == MapLocationFinderStatus.Success)
            {
                sender.ItemsSource = result.Locations;
            }
        }
    }

    private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is MapLocation location)
        {
            Map.SetCenterAsync(location.Point).FireAndForget();
        }
        else if (sender.ItemsSource is IReadOnlyList<MapLocation> { Count: > 0 } list)
        {
            Map.SetCenterAsync(list[0].Point).FireAndForget();
        }
        else if (sender.Text.Length > 2)
        {
            Map.SetCenterAsync(sender.Text).FireAndForget();
        }
    }
}
