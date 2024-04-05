using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Windows.Devices.Geolocation;
using Windows.Globalization.NumberFormatting;
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

        // Workaround: Cannot create IncrementNumberRounder as XAML resource
        DecimalFormatter cutoffFormatter = new()
        {
            IntegerDigits = 1,
            FractionDigits = 1,
            NumberRounder = new IncrementNumberRounder
            {
                Increment = .1,
                RoundingAlgorithm = RoundingAlgorithm.RoundHalfUp,
            },
        };
        DownhillCutoff.NumberFormatter = UphillCutoff.NumberFormatter = cutoffFormatter;
    }

    private TabViewItem GetTabItem(Modes mode)
    {
        return TabView.TabItems.Cast<TabViewItem>().First(item => item.Tag.Equals(mode));
    }

    private void GetMode(object item)
    {
        ViewModel.Mode = (Modes)((TabViewItem)item).Tag;
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

    private bool _ignoreTextChange;

    private async void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_ignoreTextChange)
        {
            _ignoreTextChange = false;
            return;
        }

        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            MapLocationFinderResult result = await MapLocationFinder.FindLocationsAsync(sender.Text, Map.Center);
            if (result.Status == MapLocationFinderStatus.Success)
            {
                sender.ItemsSource = result.Locations;
            }
        }
    }

    private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        _ignoreTextChange = true;
        sender.Text = ((MapLocation)args.SelectedItem).DisplayName;
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

    private void Differences_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is [TrackDifference diff, ..])
        {
            Map.ZoomTrackDifference(diff);
        }
    }

    private void Sections_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is [OnTrack section, ..])
        {
            Map.SetCenterAsync(new Geopoint((BasicGeoposition)section.TrackPoint), onlyIfNotInView: true).FireAndForget();
        }
    }

    private OnTrack _lastAddedSection;

    private void ViewModel_SectionAdded(OnTrack section)
    {
        _lastAddedSection = section;
    }

    private void Sections_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item == _lastAddedSection && !args.InRecycleQueue)
        {
            args.ItemContainer.FindDescendant<TextBox>().Focus(FocusState.Programmatic);
        }
    }

    private void Points_SelectionChanged(object _1, SelectionChangedEventArgs _2)
    {
        // TODO Center on POI?
    }
}
