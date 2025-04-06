using System.Collections.ObjectModel;
using CommunityToolkit.WinUI;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace cycloid;

public record class FileAccessEntry(string Token, string Name, string Distance, string Path, string Date);

public sealed partial class StartPage : UserControl
{
    private IStorageFile _file;
    private readonly bool _newFile;

    private ObservableCollection<FileAccessEntry> FileEntries { get; } = new();

    private StartPage()
    {
        InitializeComponent();
    }

    public StartPage(bool newFile) : this()
    {
        _newFile = newFile;
    }

    public StartPage(StorageFile file) : this()
    {
        _file = file;
    }

    private void NewFile(object _1 = null, TappedRoutedEventArgs _2 = null)
    {
        NewFileAsync().FireAndForget();

        async Task NewFileAsync()
        {
            GotoMain(await App.Current.ViewModel.NewTrackAsync());
        }
    }

    private void OpenEntry(FileAccessEntry entry)
    {
        OpenEntryAsync().FireAndForget();

        async Task OpenEntryAsync()
        {
            GotoMain(await App.Current.ViewModel.OpenTrackFileAsync(await StorageApplicationPermissions.MostRecentlyUsedList.GetFileAsync(entry.Token)));
        }
    }

    private void OpenFile(IStorageFile file)
    {
        OpenFileAsync().FireAndForget();

        async Task OpenFileAsync()
        {
            GotoMain(await App.Current.ViewModel.OpenTrackFileAsync(file));
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

        if (_newFile)
        {
            NewFile();
        }
        else if (_file is not null)
        {
            OpenFile(file: _file);
        }

        Popup.IsOpen = true;

        async Task PopulateMruListAsync()
        {
            StorageFile lastTrack = null;
            if (StorageApplicationPermissions.FutureAccessList.ContainsItem("LastTrack"))
            {
                lastTrack = await StorageApplicationPermissions.FutureAccessList.GetFileAsync("LastTrack");
            }

            foreach (AccessListEntry item in StorageApplicationPermissions.MostRecentlyUsedList.Entries)
            {
                StorageFile file = await StorageApplicationPermissions.MostRecentlyUsedList.GetFileAsync(item.Token);
                FileAccessEntry entry = new(
                    item.Token,
                    Path.GetFileNameWithoutExtension(file.Name),
                    item.Metadata,
                    Path.GetDirectoryName(file.Path),
                    $"{(await file.GetBasicPropertiesAsync()).ItemDate:d}");

                if (file.Path == lastTrack.Path)
                {
                    FileEntries.Insert(0, entry);
                }
                else
                {
                    FileEntries.Add(entry);
                }
            }

            if (FileEntries.Count > 0)
            {
                await Task.Delay(100);
                Tracks.SelectedIndex = 0;
                Tracks.Focus(FocusState.Programmatic);
            }
        }
    }

    private void Tracks_DoubleTapped(object _, DoubleTappedRoutedEventArgs e)
    {
        if (Tracks.SelectedItem is FileAccessEntry access)
        {
            OpenEntry(access);
        }
    }

    private void Tracks_PreviewKeyDown(object _, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter &&
            FocusManager.GetFocusedElement() is ListViewItem item &&
            item.FindAscendant<ListView>() == Tracks &&
            item.Content is FileAccessEntry access)
        {
            OpenEntry(access);
        }
    }
}
