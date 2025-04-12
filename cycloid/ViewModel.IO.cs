using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using cycloid.Serialization;
using FluentIcons.Common;
using Microsoft.VisualStudio.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;

namespace cycloid;

public class TrackComplete(bool isNew)
{
    public bool IsNew => isNew;
}

public record class TrackListItemPinnedChanged(TrackListItem Item);

public partial class TrackListItem : ObservableObject
{
    private string _token;
    private bool _updateOnChange;

    public StorageFile File { get; private set; }

    public DateTime LastAccessDate { get; private set; }

    public float TrackDistance 
    { 
        get;
        set
        {
            field = value;
            Update();
        }
    }

    [ObservableProperty]
    public partial bool IsPinned { get; set; }

    partial void OnIsPinnedChanged(bool value)
    {
        Update(updateAccessDate: false);
        StrongReferenceMessenger.Default.Send(new TrackListItemPinnedChanged(this));
    }
    
    public string Name => Path.GetFileNameWithoutExtension(File.Name);

    public string DirectoryPath => Path.GetDirectoryName(File.Path);

    public void Update(bool updateAccessDate = true)
    {
        if (!_updateOnChange)
        {
            return;
        }

        if (updateAccessDate)
        {
            LastAccessDate = DateTime.Now;
        }

        string metadata = Encode();

        if (_token is null)
        {
            _token = StorageApplicationPermissions.MostRecentlyUsedList.Add(File, metadata);
        }
        else
        {
            StorageApplicationPermissions.MostRecentlyUsedList.AddOrReplace(_token, File, metadata);
        }
    }

    public void Delete()
    {
        if (_token is not null)
        {
            StorageApplicationPermissions.MostRecentlyUsedList.Remove(_token);
            _token = null;
        }
        File = null;
    }

    public static async Task<TrackListItem> CreateAsync(AccessListEntry entry)
    {
        StorageFile file = await StorageApplicationPermissions.MostRecentlyUsedList.GetFileAsync(entry.Token);
        (DateTime lastAccess, float trackDistance, bool isPinned) = Decode(entry.Metadata);

        return new TrackListItem
        {
            _token = entry.Token,
            File = file,
            TrackDistance = trackDistance,
            LastAccessDate = lastAccess,
            IsPinned = isPinned,
            _updateOnChange = true,
        };
    }

    public static TrackListItem Create(StorageFile file)
    {
        return new TrackListItem 
        { 
            File = file, 
            LastAccessDate = DateTime.Now,
            _updateOnChange = true,
        };
    }

    private string Encode()
    {
        return string.Create(NumberFormatInfo.InvariantInfo, $"{LastAccessDate.Ticks}|{TrackDistance:F1}|{(IsPinned ? 1 : 0)}");
    }

    private static (DateTime LastAccess, float TrackDistance, bool IsPinned) Decode(ReadOnlySpan<char> metadata)
    {
        Span<Range> ranges = stackalloc Range[3];
        metadata.Split(ranges, '|');

        return (new DateTime(long.Parse(metadata[ranges[0]])), float.Parse(metadata[ranges[1]], NumberFormatInfo.InvariantInfo), metadata[ranges[2]].SequenceEqual("1"));
    }

    public IconVariant ToIconVariant(bool isPinned)
    {
        return isPinned ? IconVariant.Filled : IconVariant.Regular;
    }
}

public class InitializeTrackOptions
{
    public TrackListItem TrackItem { get; init; }
    public StorageFile File { get; init; }
    public string FilePath { get; init; }
    public bool CreateFile { get; init; }
}

partial class ViewModel : IRecipient<TrackListItemPinnedChanged>
{
    private readonly SemaphoreSlim _saveTrackSemaphore = new(1);
    private CancellationTokenSource _saveTrackCts = new();
    private int _saveCounter;

    public ObservableCollection<TrackListItem> TrackListItems { get; } = [];

    public async Task PopulateTrackListAsync()
    {
        TrackListItem[] items = await Task.WhenAll(
            StorageApplicationPermissions.MostRecentlyUsedList
                .Entries
                .Select(TrackListItem.CreateAsync));

        foreach (TrackListItem item in items.OrderByDescending(item => item.IsPinned).ThenByDescending(item => item.LastAccessDate))
        {
            if (item.IsPinned)
            {
                // Touch all pinned items to prevent them from disapearing
                item.Update(updateAccessDate: false);
            }
            TrackListItems.Add(item);
        }

        await App.Current.UpdateJumpListAsync(TrackListItems);
        TrackListItems.CollectionChanged += (_, _) => App.Current.UpdateJumpListAsync(TrackListItems).FireAndForget();
    }

    public TrackListItem GetTrackListItem(StorageFile file)
    {
        TrackListItem trackListItem = TrackListItems.FirstOrDefault(item => item.File.Path == file.Path);
        if (trackListItem is null)
        {
            trackListItem = TrackListItem.Create(file);
        }

        return trackListItem;
    }

    public async Task<InitializeTrackOptions> OpenTrackAsync()
    {
        FileOpenPicker picker = new()
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".track");
        StorageFile file = await picker.PickSingleFileAsync();

        if (file is null)
        {
            return null;
        }

        if (Program.RegisterForFile(file.Path, out _))
        {
            return new InitializeTrackOptions { File = file };
        }
        else
        {
            await ShowFileAlreadyOpenAsync(file.Name);

            return null;
        }
    }

    public async Task<InitializeTrackOptions> OpenTrackFileAsync(TrackListItem trackItem)
    {
        if (Program.RegisterForFile(trackItem.File.Path, out _))
        {
            return new InitializeTrackOptions { TrackItem = trackItem };
        }
        else
        {
            await ShowFileAlreadyOpenAsync(trackItem.File.Name);
            
            return null;
        }
    }

    public async Task<InitializeTrackOptions> CreateTrackAsync()
    {
        FileSavePicker picker = new()
        {
            SuggestedFileName = "New Track",
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        // TODO CsWinRT 2.3
        picker.FileTypeChoices.Add("Track", (string[])[".track"]);
        StorageFile file = await picker.PickSaveFileAsync();

        if (file is null)
        {
            return null;
        }

        if (Program.RegisterForFile(file.Path, out _))
        {
            return new InitializeTrackOptions { File = file, CreateFile = true };
        }
        else
        {
            await ShowFileAlreadyOpenAsync(file.Name);

            return null;
        }
    }

    public async Task LoadFileAsync(InitializeTrackOptions options)
    {
        await PopulateTrackListAsync();

        TrackItem = 
            options.TrackItem
            ?? GetTrackListItem(
                options.File
                ?? await StorageFile.GetFileFromPathAsync(options.FilePath));

        Track = new Track(options.CreateFile);

        Stopwatch watch = Stopwatch.StartNew();

        await (options.CreateFile ? CreateAsync() : LoadAsync());

        Status = $"{TrackName} {(options.CreateFile ? "created" : "opened")} ({watch.ElapsedMilliseconds} ms)";
        StrongReferenceMessenger.Default.Send(new TrackComplete(false));

        TrackItem.TrackDistance = Track.Points.Total.Distance;

        async Task CreateAsync()
        {
            await TaskScheduler.Default;

            StorageFile tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("current", CreationCollisionOption.ReplaceExisting);
            using (Stream stream = await tempFile.OpenStreamForWriteAsync().ConfigureAwait(false))
            {
                await Serializer.SerializeAsync(stream, Track, default).ConfigureAwait(false);
            }

            await tempFile.CopyAndReplaceAsync(TrackItem.File);
        }

        async Task LoadAsync()
        {
            SynchronizationContext ui = SynchronizationContext.Current;
            await TaskScheduler.Default;

            using Stream stream = await TrackItem.File.OpenStreamForReadAsync().ConfigureAwait(false);

            await Serializer.LoadAsync(stream, Track, ui).ConfigureAwait(false);
        }
    }

    public async Task SaveTrackAsync()
    {
        if (Track is not null)
        {
            await _saveTrackCts.CancelAsync();
            _saveTrackCts = new CancellationTokenSource();
            CancellationToken cancellationToken = _saveTrackCts.Token;
            try
            {
                await _saveTrackSemaphore.WaitAsync(cancellationToken);

                Stopwatch watch = Stopwatch.StartNew();

                await SaveAsync();

                Status = $"{TrackName} saved ({watch.ElapsedMilliseconds} ms) {++_saveCounter}";

                TrackItem.TrackDistance = Track.Points.Total.Distance;

                async Task SaveAsync()
                {
                    await TaskScheduler.Default;

                    StorageFile tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(Guid.NewGuid().ToString(), CreationCollisionOption.ReplaceExisting);
                    using (Stream stream = await tempFile.OpenStreamForWriteAsync().ConfigureAwait(false))
                    {
                        await Serializer.SerializeAsync(stream, Track, cancellationToken).ConfigureAwait(false);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    await tempFile.MoveAndReplaceAsync(TrackItem.File);
                }
            }
            catch (OperationCanceledException)
            { }
            finally
            {
                _saveTrackSemaphore.Release();
            }
        }
    }

    private static async Task ShowFileAlreadyOpenAsync(string fileName)
    {
        await new ContentDialog
        {
            Title = "Cannot open file",
            Content = $"{fileName} is already open.",
            CloseButtonText = "Close",
        }.ShowAsync();
    }

    public void Receive(TrackListItemPinnedChanged message)
    {
        int oldIndex = TrackListItems.IndexOf(message.Item);
        if (oldIndex == -1)
        {
            return;
        }

        IEnumerable<TrackListItem> relevantItems = TrackListItems.Where(item => item != message.Item && item.IsPinned == message.Item.IsPinned);
        (int newIndex, TrackListItem nextItem) = relevantItems
            .Index()
            .FirstOrDefault(tuple => 
                tuple.Item != message.Item && 
                tuple.Item.LastAccessDate < message.Item.LastAccessDate);

        if (nextItem is null)
        {
            newIndex = relevantItems.Count();
        }
        if (!message.Item.IsPinned)
        {
            newIndex += TrackListItems.Count(item => item.IsPinned);
        }

        if (oldIndex != newIndex)
        {
            TrackListItems.Move(oldIndex, newIndex);
        }
    }
}