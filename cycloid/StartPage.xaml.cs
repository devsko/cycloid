using System.Collections.ObjectModel;
using CommunityToolkit.WinUI;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace cycloid;


public sealed partial class StartPage : UserControl
{
    private readonly bool _createFile;

    public StartPage(bool createFile)
    {
        _createFile = createFile;
        InitializeComponent();
    }

    private void CreateFile(object _1 = null, TappedRoutedEventArgs _2 = null)
    {
        CreateFileAsync().FireAndForget();

        async Task CreateFileAsync()
        {
            GotoMain(await App.Current.ViewModel.CreateTrackAsync());
        }
    }

    private void OpenEntry(TrackListItem entry)
    {
        OpenEntryAsync().FireAndForget();

        async Task OpenEntryAsync()
        {
            GotoMain(await App.Current.ViewModel.OpenTrackFileAsync(entry));
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

    private void GotoMain(bool result)
    {
        if (result)
        {
            Popup.IsOpen = false;
            Window.Current.Content = new MainPage();
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        PopulateMruListAsync().FireAndForget();

        if (_createFile)
        {
            CreateFile();
        }

        Popup.IsOpen = true;

        async Task PopulateMruListAsync()
        {
            await App.Current.ViewModel.PopulateTrackListAsync();

            if (App.Current.ViewModel.TrackListItems.Count > 0)
            {
                await Task.Delay(100);
                Tracks.SelectedIndex = 0;
                Tracks.Focus(FocusState.Programmatic);
            }
        }
    }

    private void Tracks_DoubleTapped(object _, DoubleTappedRoutedEventArgs e)
    {
        if (Tracks.SelectedItem is TrackListItem access)
        {
            OpenEntry(access);
        }
    }

    private void Tracks_PreviewKeyDown(object _, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter &&
            FocusManager.GetFocusedElement() is ListViewItem item &&
            item.FindAscendant<ListView>() == Tracks &&
            item.Content is TrackListItem access)
        {
            OpenEntry(access);
        }
    }
}
