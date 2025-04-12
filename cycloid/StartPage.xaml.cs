using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace cycloid;


public sealed partial class StartPage : UserControl,
    IRecipient<TrackListItemPinnedChanged>
{
    private readonly bool _createFile;

    public StartPage(bool createFile)
    {
        _createFile = createFile;
        InitializeComponent();

        StrongReferenceMessenger.Default.Register<TrackListItemPinnedChanged>(this);
    }

    private void OpenEntry(TrackListItem entry)
    {
        OpenEntryAsync().FireAndForget();

        async Task OpenEntryAsync()
        {
            GotoMain(await App.Current.ViewModel.OpenTrackAsync(entry));
        }
    }

    private void CreateFile(object _1 = null, TappedRoutedEventArgs _2 = null)
    {
        CreateFileAsync().FireAndForget();

        async Task CreateFileAsync()
        {
            GotoMain(await App.Current.ViewModel.CreateTrackAsync("New Track"));
        }
    }

    private void OpenFile(object _1 = null, TappedRoutedEventArgs _2 = null)
    {
        OpenFileAsync().FireAndForget();

        async Task OpenFileAsync()
        {
            GotoMain(await App.Current.ViewModel.OpenTrackAsync());
        }
    }

    private void GotoMain(InitializeTrackOptions options)
    {
        if (options is not null)
        {
            Popup.IsOpen = false;
            Window.Current.Content = new MainPage(options);
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        SelectFirstTrackListItemAsync().FireAndForget();

        if (_createFile)
        {
            CreateFile();
        }

        Popup.IsOpen = true;

        async Task SelectFirstTrackListItemAsync()
        {
            await App.Current.ViewModel.PopulateTrackListAsync();

            if (App.Current.ViewModel.TrackListItems.Count > 0)
            {
                await Task.Delay(100);
                TrackList.SelectedIndex = 0;
                TrackList.Focus(FocusState.Programmatic);
            }
        }
    }

    private void TrackList_DoubleTapped(object _, DoubleTappedRoutedEventArgs e)
    {
        if (TrackList.SelectedItem is TrackListItem access)
        {
            OpenEntry(access);
        }
    }

    private void TrackList_PreviewKeyDown(object _, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter &&
            FocusManager.GetFocusedElement() is ListViewItem item &&
            item is { Content: TrackListItem access } &&
            item.FindAscendant<ListView>() == TrackList)
        {
            OpenEntry(access);
        }
    }

    private void TrackList_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
    {
        if (args.ItemContainer is null)
        {
            ListViewItem container = new()
            {
                Style = sender.ItemContainerStyle,
                ContentTemplate = sender.ItemTemplate,
            };
            container.Loaded += ItemsContainer_Loaded;
            container.PointerEntered += ItemsContainer_PointerEntered;
            container.PointerExited += ItemsContainer_PointerExited;

            args.ItemContainer = container;
            args.IsContainerPrepared = true;
        }
    }

    private void ItemsContainer_Loaded(object sender, RoutedEventArgs e)
    {
        SelectorItem container = (SelectorItem)sender;
        ToggleButton button = ((FrameworkElement)container.ContentTemplateRoot).FindChild<ToggleButton>();
        button.PointerExited += Button_PointerExited;
    }

    private void ItemsContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        SelectorItem container = (SelectorItem)sender;
        ToggleButton button = ((FrameworkElement)container.ContentTemplateRoot).FindChild<ToggleButton>();
        if (button.IsChecked is false)
        {
            VisualStateManager.GoToState(button, "Visible", true);
        }
    }

    private void ItemsContainer_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        SelectorItem container = (SelectorItem)sender;
        ToggleButton button = ((FrameworkElement)container.ContentTemplateRoot).FindChild<ToggleButton>();
        if (button.IsChecked is false)
        {
            VisualStateManager.GoToState(button, "Normal", true);
        }
    }

    private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        ToggleButton button = (ToggleButton)sender;
        if (button.IsChecked is false)
        {
            VisualStateManager.GoToState(button, "Visible", true);
        }
    }

    public void Receive(TrackListItemPinnedChanged message)
    {
        TrackList.SelectedItem = message.Item;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (((MenuFlyoutItem)sender).DataContext is TrackListItem access)
        {
            OpenEntry(access);
        }
    }

    private void CreateCopy_Click(object sender, RoutedEventArgs e)
    {
        if (((MenuFlyoutItem)sender).DataContext is TrackListItem access)
        {
            CopyEntryAsync().FireAndForget();

            async Task CopyEntryAsync()
            {
                GotoMain(await App.Current.ViewModel.CopyTrackAsync(access));
            }
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (((MenuFlyoutItem)sender).DataContext is TrackListItem access)
        {
            DataPackage data = new();
            data.SetText(access.File.Path);
            Clipboard.SetContent(data);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (((MenuFlyoutItem)sender).DataContext is TrackListItem access)
        {
            App.Current.ViewModel.TrackListItems.Remove(access);
            access.Delete();
        }
    }
}
