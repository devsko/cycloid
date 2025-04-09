using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using cycloid.Serialization;
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

    [ObservableProperty]
    public partial float TrackDistance { get; set; }

    partial void OnTrackDistanceChanged(float value)
    {
        Update();
    }

    [ObservableProperty]
    public partial bool IsPinned { get; set; }

    partial void OnIsPinnedChanged(bool value)
    {
        Update(updateAccessDate: false);
        StrongReferenceMessenger.Default.Send(new TrackListItemPinnedChanged(this));
    }
    
    public string Name => Path.GetFileNameWithoutExtension(File.Name);

    public string FilePath => Path.GetDirectoryName(File.Path);

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
            LastAccessDate = DateTime.Now 
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
            TrackListItems.Add(item);
        }
    }

    public async Task<bool> OpenTrackAsync()
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
            return false;
        }

        return await OpenTrackFileAsync(TrackListItem.Create(file));
    }

    public async Task<bool> OpenTrackFileAsync(TrackListItem item)
    {
        if (Program.RegisterForFile(item.File, out _))
        {
            TrackItem = item;

            return true;
        }
        else
        {
            await ShowFileAlreadyOpenAsync(item.File);
            
            return false;
        }
    }

    public async Task<bool> CreateTrackAsync()
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
            return false;
        }

        if (Program.RegisterForFile(file, out _))
        {
            TrackItem = TrackListItem.Create(file);
            _creteFile = true;

            return true;
        }
        else
        {
            await ShowFileAlreadyOpenAsync(file);

            return false;
        }
    }

    public async Task LoadFileAsync()
    {
        if (_creteFile)
        {
            Track = new Track(true);

            await SaveAsync();

            Status = $"{TrackName} created";
            StrongReferenceMessenger.Default.Send(new TrackComplete(true));

            TrackItem.Update();

            async Task SaveAsync()
            {
                await TaskScheduler.Default;

                StorageFile tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("current", CreationCollisionOption.ReplaceExisting);
                using (Stream stream = await tempFile.OpenStreamForWriteAsync().ConfigureAwait(false))
                {
                    await Serializer.SerializeAsync(stream, Track, default).ConfigureAwait(false);
                }

                await tempFile.CopyAndReplaceAsync(TrackItem.File);
            }
        }
        else
        {
            Track = new Track(false);

            Stopwatch watch = Stopwatch.StartNew();

            await LoadAsync();

            Status = $"{TrackName} opened ({watch.ElapsedMilliseconds} ms)";
            StrongReferenceMessenger.Default.Send(new TrackComplete(false));

            TrackItem.TrackDistance = Track.Points.Total.Distance;

            async Task LoadAsync()
            {
                SynchronizationContext ui = SynchronizationContext.Current;
                await TaskScheduler.Default;

                using Stream stream = await TrackItem.File.OpenStreamForReadAsync().ConfigureAwait(false);

                await Serializer.LoadAsync(stream, Track, ui).ConfigureAwait(false);
            }
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

    private static async Task ShowFileAlreadyOpenAsync(IStorageFile file)
    {
        await new ContentDialog
        {
            Title = "Cannot open file",
            Content = $"{file.Name} is already open.",
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