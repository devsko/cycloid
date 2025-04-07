using System.Diagnostics;
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

partial class ViewModel
{
    private readonly SemaphoreSlim _saveTrackSemaphore = new(1);
    private CancellationTokenSource _saveTrackCts = new();
    private int _saveCounter;

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

        return await OpenTrackFileAsync(file);
    }

    public async Task<bool> OpenTrackFileAsync(IStorageFile file)
    {
        if (Program.RegisterForFile(file, out _))
        {
            File = file;

            return true;
        }
        else
        {
            await ShowFileAlreadyOpenAsync(file);
            
            return false;
        }
    }

    public async Task<bool> NewTrackAsync()
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
            if (StorageApplicationPermissions.FutureAccessList.ContainsItem("LastTrack"))
            {
                StorageApplicationPermissions.FutureAccessList.Remove("LastTrack");
            }
            File = file;
            _fileIsNew = true;

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
        if (_fileIsNew)
        {
            Track = new Track(true);

            await SaveAsync();

            Status = $"{TrackName} created";
            StrongReferenceMessenger.Default.Send(new TrackComplete(true));

            StorageApplicationPermissions.FutureAccessList.AddOrReplace("LastTrack", File);
            StorageApplicationPermissions.MostRecentlyUsedList.Add(File, "", RecentStorageItemVisibility.AppAndSystem);

            async Task SaveAsync()
            {
                await TaskScheduler.Default;

                StorageFile tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("current", CreationCollisionOption.ReplaceExisting);
                using (Stream stream = await tempFile.OpenStreamForWriteAsync().ConfigureAwait(false))
                {
                    await Serializer.SerializeAsync(stream, Track, default).ConfigureAwait(false);
                }

                await tempFile.CopyAndReplaceAsync(File);
            }
        }
        else
        {
            Track = new Track(false);

            Stopwatch watch = Stopwatch.StartNew();

            await LoadAsync();

            Status = $"{TrackName} opened ({watch.ElapsedMilliseconds} ms)";
            StrongReferenceMessenger.Default.Send(new TrackComplete(false));

            StorageApplicationPermissions.FutureAccessList.AddOrReplace("LastTrack", File);
            StorageApplicationPermissions.MostRecentlyUsedList.Add(File, $"{Format.Distance(Track.Points.Total.Distance)}", RecentStorageItemVisibility.AppAndSystem);

            async Task LoadAsync()
            {
                SynchronizationContext ui = SynchronizationContext.Current;
                await TaskScheduler.Default;

                using Stream stream = await File.OpenStreamForReadAsync().ConfigureAwait(false);

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

                StorageApplicationPermissions.MostRecentlyUsedList.Add(File, $"{Format.Distance(Track.Points.Total.Distance)}", RecentStorageItemVisibility.AppAndSystem);
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("LastTrack", File);

                async Task SaveAsync()
                {
                    await TaskScheduler.Default;

                    StorageFile tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(Guid.NewGuid().ToString(), CreationCollisionOption.ReplaceExisting);
                    using (Stream stream = await tempFile.OpenStreamForWriteAsync().ConfigureAwait(false))
                    {
                        await Serializer.SerializeAsync(stream, Track, cancellationToken).ConfigureAwait(false);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    await tempFile.MoveAndReplaceAsync(File);
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
}