﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        ViewModel.ToggleHeatmapVisibleAsync().FireAndForget();
        InitialSceneAsync().FireAndForget();

        async Task InitialSceneAsync()
        {
            await Map.SetCenterAsync("Stampfl Samerberg", 8);
            await ViewModel.OpenLastTrackAsync();
        }
    }

    private void ViewModel_TrackChanged(Track _, Track track)
    {
        ApplicationView.GetForCurrentView().Title = track is null ? string.Empty : track.Name;
    }

    private async void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            MapLocationFinderResult result = await MapLocationFinder.FindLocationsAsync(sender.Text, Map.Center);
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

    private void Differences_SelectionChanged(object _1, SelectionChangedEventArgs _2)
    {
        Map.ZoomTrackDifference(((TrackDifference)Differences.SelectedItem));
    }
}
