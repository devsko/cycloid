using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using cycloid.Info;
using Microsoft.UI.Xaml.Controls;
using Windows.Devices.Geolocation;
using Windows.Globalization.NumberFormatting;
using Windows.Services.Maps;
using Windows.Storage;
using Windows.System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace cycloid;

public sealed partial class MainPage : Page,
    IRecipient<FileChanged>,
    IRecipient<OnTrackAdded>
{
    private IStorageFile _initialFile;
    private bool _ignoreTextChange;
    private OnTrack _lastAddedOnTrack;

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

        StrongReferenceMessenger.Default.Register<FileChanged>(this);
        StrongReferenceMessenger.Default.Register<OnTrackAdded>(this);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is IStorageFile file)
        {
            _initialFile = file;
        }
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
        ViewModel.ToggleHeatmapVisibleCommand.Execute(null);
        OpenTrackAsync().FireAndForget();

        async Task OpenTrackAsync()
        {
            if (_initialFile is not null)
            {
                await ViewModel.OpenTrackFileAsync(_initialFile);
                _initialFile = null;
            }
            else
            {
                await ViewModel.OpenLastTrackAsync();
            }
        }
    }

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
        Geopoint point;
        if (args.ChosenSuggestion is MapLocation location)
        {
            point = location.Point;
        }
        else if (sender.ItemsSource is IReadOnlyList<MapLocation> list && list is [MapLocation first, ..])
        {
            point = first.Point;
        }
        else
        {
            return;
        }

        StrongReferenceMessenger.Default.Send(new SetMapCenterMessage((MapPoint)point.Position));
    }

    private void PoisButton_CategoryChanged(InfoCategory category, bool value)
    {
        ViewModel.SetInfoCategoryVisible(true, category, value);
    }

    private void InfoButton_CategoryChanged(InfoCategory category, bool value)
    {
        ViewModel.SetInfoCategoryVisible(false, category, value);
    }

    private void Differences_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is [TrackDifference diff, ..])
        {
            Map.ZoomTrackDifference(diff);
        }
    }

    private void OnTracks_ContainerContentChanging(ListViewBase _, ContainerContentChangingEventArgs args)
    {
        Debug.WriteLine($"ContainerContentChanging '{(args.Item is null ? "null" : ((OnTrack)args.Item).Name)}'");
        if (args.Item is not null && args.Item == _lastAddedOnTrack)
        {
            Debug.WriteLine("Focus Text");
            TextBox textBox = args.ItemContainer.FindDescendant<TextBox>();
            textBox.Focus(FocusState.Programmatic);
            textBox.Text = _lastAddedOnTrack.Name;
            textBox.SelectAll();
            _lastAddedOnTrack = null;
        }
    }

    private void OnTracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            ((ListView)sender).ScrollIntoView(e.AddedItems[0]);
        }
    }

    private void OnTracks_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        ListView list = (ListView)sender;
        if (list.SelectedItem is OnTrack onTrack)
        {
            SendMapCenterMessage(onTrack);
        }
    }

    private void OnTracks_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        ListView list = (ListView)sender;
        if (e.Key == VirtualKey.Enter && 
            FocusManager.GetFocusedElement() is ListViewItem item &&
            item.FindAscendant<ListView>() == list &&
            item.Content is OnTrack onTrack)
        {
            SendMapCenterMessage(onTrack);
        }
    }

    private void SendMapCenterMessage(OnTrack onTrack)
    {
        StrongReferenceMessenger.Default.Send(new SetMapCenterMessage(onTrack.Location));
    }

    private void TextBox_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        if (args.Character is '\r')
        {
            sender.FindAscendant<ListView>().Focus(FocusState.Programmatic);
            args.Handled = true;
        }
    }

    void IRecipient<FileChanged>.Receive(FileChanged message)
    {
        ApplicationView.GetForCurrentView().Title = message.Value is null ? string.Empty : message.Value.Name;
    }

    void IRecipient<OnTrackAdded>.Receive(OnTrackAdded message)
    {
        Debug.WriteLine($"OnTrackAdded {message.OnTrack}");
        _lastAddedOnTrack = message.OnTrack;
    }
}
