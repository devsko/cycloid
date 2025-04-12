using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using cycloid.Controls;
using cycloid.Info;
using Microsoft.UI.Xaml.Controls;
using Windows.Globalization.NumberFormatting;
using Windows.Services.Maps;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using WinRT;

namespace cycloid;

public sealed partial class MainPage : UserControl,
    IRecipient<OnTrackAdded>,
    IRecipient<RequestPasteSelectionDetails>,
    IRecipient<RequestDeleteSelectionDetails>,
    IRecipient<RequestExportDetails>,
    IRecipient<DragWayPointEnded>,
    IRecipient<TitleBarLayoutChanged>
{
    private readonly InitializeTrackOptions _initializeTrackOptions;
    private bool _ignoreTextChange;
    private OnTrack _lastAddedOnTrack;
    private bool _delayZoomCurrentTrackDifference;

    public MainPage(InitializeTrackOptions options)
    {
        _initializeTrackOptions = options;

        InitializeComponent();

        Window.Current.SetTitleBar(TitleBar);
        UpdateTitleBarLayout();

        Window.Current.CoreWindow.Activated += CoreWindow_Activated;

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

        StrongReferenceMessenger.Default.Register<OnTrackAdded>(this);
        StrongReferenceMessenger.Default.Register<RequestPasteSelectionDetails>(this);
        StrongReferenceMessenger.Default.Register<RequestDeleteSelectionDetails>(this);
        StrongReferenceMessenger.Default.Register<RequestExportDetails>(this);
        StrongReferenceMessenger.Default.Register<DragWayPointEnded>(this);
        StrongReferenceMessenger.Default.Register<TitleBarLayoutChanged>(this);
    }

    private ViewModel ViewModel => App.Current.ViewModel;

    private void UpdateTitleBarLayout()
    {
        var margin = (Thickness)Resources["TitleBarMargin"];

        TitleBarControlsRoot.Margin = new Thickness(margin.Left + App.Current.TitleBarInset.X, margin.Top, margin.Right + App.Current.TitleBarInset.Y, margin.Bottom);
        TitleColumn.Width = new GridLength(TitleColumn.Width.Value - App.Current.TitleBarInset.X);
    }

    private TabViewItem GetTabItem(Modes mode)
    {
        return TabView.TabItems.Cast<TabViewItem>().First(item => item.Tag.Equals(mode));
    }

    private void GetMode(object item)
    {
        ViewModel.Mode = (Modes)((TabViewItem)item).Tag;
    }

    private void CoreWindow_Activated(CoreWindow sender, WindowActivatedEventArgs args)
    {
        TitleTextBlock.Opacity = args.WindowActivationState == CoreWindowActivationState.Deactivated ? .5 : 1;
    }

    private void Page_Loaded(object _1, RoutedEventArgs _2)
    {
        ViewModel.ToggleHeatmapVisibleCommand.Execute(null);

        ViewModel.LoadFileAsync(_initializeTrackOptions).FireAndForget();
    }

    [GeneratedBindableCustomProperty([nameof(DisplayName)], null)]
    public partial class MapLocationWrapper(MapLocation location)
    {
        public string DisplayName => location.DisplayName;
        public MapLocation Location => location;
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
                sender.ItemsSource = result.Locations.Select(location => new MapLocationWrapper(location)).ToList();
            }
        }
    }

    private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        _ignoreTextChange = true;
        sender.Text = ((MapLocationWrapper)args.SelectedItem).DisplayName;
    }

    private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        MapLocation location;
        if (args.ChosenSuggestion is MapLocationWrapper wrapper)
        {
            location = wrapper.Location;
        }
        else if (sender.ItemsSource is IReadOnlyList<MapLocationWrapper> list && list is [MapLocationWrapper first, ..])
        {
            location = first.Location;
        }
        else
        {
            return;
        }

        StrongReferenceMessenger.Default.Send(new BringLocationIntoViewMessage(location.Point.Position.ToMapPoint()));
    }

    private void PoisButton_CategoryChanged(InfoCategory category, bool value)
    {
        ViewModel.SetInfoCategoryVisible(true, category, value);
    }

    private void InfoButton_CategoryChanged(InfoCategory category, bool value)
    {
        ViewModel.SetInfoCategoryVisible(false, category, value);
    }

    private void Differences_SelectionChanged(object _, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is [TrackDifference diff, ..])
        {
            if (ViewModel.IsDraggingWayPoint)
            {
                _delayZoomCurrentTrackDifference = true;
            }
            else
            {
                Map.ZoomTrackDifference(diff);
            }
        }
        else if (e.RemovedItems is not [])
        {
            _delayZoomCurrentTrackDifference = false;
        }
    }

    private void OnTracks_ContainerContentChanging(ListViewBase _, ContainerContentChangingEventArgs args)
    {
        if (args.Item is not null && args.Item == _lastAddedOnTrack)
        {
            TextBox textBox = ((FrameworkElement)args.ItemContainer.ContentTemplateRoot).FindChild<TextBox>();
            textBox.Focus(FocusState.Programmatic);
            textBox.Text = _lastAddedOnTrack.Name;
            textBox.SelectAll();
            _lastAddedOnTrack = null;
        }
    }

    private void OnTracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is [ OnTrack onTrack, .. ])
        {
            ((ListView)sender).ScrollIntoView(onTrack);
            if (!onTrack.PointOfInterest.IsSection && !onTrack.IsOffTrack)
            {
                ViewModel.CurrentPoint = onTrack.TrackPoint;
            }
        }
    }

    private void OnTracks_DoubleTapped(object sender, DoubleTappedRoutedEventArgs _)
    {
        ListView list = (ListView)sender;
        if (list.SelectedItem is OnTrack onTrack)
        {
            BringOnTrackIntoView(onTrack);
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
            BringOnTrackIntoView(onTrack);
        }
    }

    private void BringOnTrackIntoView(OnTrack onTrack)
    {
        if (onTrack.IsOffTrack)
        {
            StrongReferenceMessenger.Default.Send(new BringLocationIntoViewMessage(onTrack.Location));
        }
        else if (onTrack.PointOfInterest.IsSection)
        {
            StrongReferenceMessenger.Default.Send(new BringTrackIntoViewMessage(onTrack.TrackPoint, onTrack.GetPrevious()?.TrackPoint ?? ViewModel.Track.Points.First()));
        }
        else
        {
            StrongReferenceMessenger.Default.Send(new BringTrackIntoViewMessage(onTrack.TrackPoint));
        }
    }

    private void TextBox_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        if (args.Character is '\r')
        {
            sender.FindAscendant<ListView>().Focus(FocusState.Programmatic);
            args.Handled = true;
        }
    }

    void IRecipient<OnTrackAdded>.Receive(OnTrackAdded message)
    {
        _lastAddedOnTrack = message.Value;
    }

    void IRecipient<DragWayPointEnded>.Receive(DragWayPointEnded message)
    {
        if (_delayZoomCurrentTrackDifference)
        {
            Map.ZoomTrackDifference(ViewModel.Track?.CompareSession?.CurrentDifference);
            _delayZoomCurrentTrackDifference = false;
        }
    }

    void IRecipient<TitleBarLayoutChanged>.Receive(TitleBarLayoutChanged message)
    {
        UpdateTitleBarLayout();
    }

    void IRecipient<RequestPasteSelectionDetails>.Receive(RequestPasteSelectionDetails message)
    {
        message.Reply(new PasteSelectionDialog(message.Selection, message.PasteAt).GetResultAsync());
    }

    void IRecipient<RequestDeleteSelectionDetails>.Receive(RequestDeleteSelectionDetails message)
    {
        message.Reply(new DeleteSelectionDialog(message.Selection).GetResultAsync());
    }

    void IRecipient<RequestExportDetails>.Receive(RequestExportDetails message)
    {
        message.Reply(new ExportDialog(message).GetResultAsync());
    }
}
