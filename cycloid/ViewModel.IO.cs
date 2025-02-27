using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using cycloid.Serialization;
using Microsoft.VisualStudio.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;

namespace cycloid;

public class FileChanged(IStorageFile value) : ValueChangedMessage<IStorageFile>(value);

public class TrackComplete(bool isNew)
{
    public bool IsNew => isNew;
}

partial class ViewModel
{
    private readonly SemaphoreSlim _saveTrackSemaphore = new(1);
    private CancellationTokenSource _saveTrackCts = new();
    private int _saveCounter;

    [RelayCommand]
    public async Task OpenTrackAsync()
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
            return;
        }

        await OpenTrackFileAsync(file);
    }

    public async Task OpenLastTrackAsync()
    {
        if (StorageApplicationPermissions.FutureAccessList.ContainsItem("LastTrack"))
        {
            try
            {
                StorageFile file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync("LastTrack");
                await OpenTrackFileAsync(file, dontShowDialog: true);
            }
            catch (FileNotFoundException)
            {
                StorageApplicationPermissions.FutureAccessList.Remove("LastTrack");
            }
        }
    }

    public async Task OpenTrackFileAsync(IStorageFile file, bool dontShowDialog = false)
    {
        StorageApplicationPermissions.MostRecentlyUsedList.Add(file, "", RecentStorageItemVisibility.AppAndSystem);

        if (Program.RegisterForFile(file, out _))
        {
            StorageApplicationPermissions.FutureAccessList.AddOrReplace("LastTrack", file);
            await LoadTrackFileAsync(file);
        }
        else if (!dontShowDialog)
        {
            await ShowFileAlreadyOpenAsync(file);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveTrackAs))]
    public async Task SaveTrackAsAsync()
    {
        FileSavePicker picker = new()
        {
            SuggestedFileName = "Save Track",
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        // TODO CsWinRT 2.3
        picker.FileTypeChoices.Add("Track", (string[])[".track"]);
        StorageFile file = await picker.PickSaveFileAsync();

        if (file is null)
        {
            return;
        }

        StorageApplicationPermissions.MostRecentlyUsedList.Add(file, "", RecentStorageItemVisibility.AppAndSystem);

        if (!Program.RegisterForFile(file, out _))
        {
            await ShowFileAlreadyOpenAsync(file);
        }
        else
        {
            await File.CopyAndReplaceAsync(file);
            File = file;
        }
    }

    private bool CanSaveTrackAs() => HasTrack;

    [RelayCommand]
    public async Task NewTrackAsync()
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
            return;
        }

        StorageApplicationPermissions.MostRecentlyUsedList.Add(file, "", RecentStorageItemVisibility.AppAndSystem);

        if (Program.RegisterForFile(file, out _))
        {
            if (StorageApplicationPermissions.FutureAccessList.ContainsItem("LastTrack"))
            {
                StorageApplicationPermissions.FutureAccessList.Remove("LastTrack");
            }

            Track = new Track(true);
            File = file;

            await SaveAsync();

            StrongReferenceMessenger.Default.Send(new TrackComplete(true));

            Status = $"{Track.Name} created";

            StorageApplicationPermissions.FutureAccessList.AddOrReplace("LastTrack", File);

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
            await ShowFileAlreadyOpenAsync(file);
        }
    }

    [RelayCommand]
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

                Status = $"{Track.Name} saved ({watch.ElapsedMilliseconds} ms) {++_saveCounter}";

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

    private async Task LoadTrackFileAsync(IStorageFile file)
    {
        Track = new Track(false);
        File = file;

        Stopwatch watch = Stopwatch.StartNew();

        await LoadAsync();

        Status = $"{Track.Name} opened ({watch.ElapsedMilliseconds} ms)";

        StrongReferenceMessenger.Default.Send(new TrackComplete(false));

        async Task LoadAsync()
        {
            SynchronizationContext ui = SynchronizationContext.Current;
            await TaskScheduler.Default;

            using Stream stream = await File.OpenStreamForReadAsync().ConfigureAwait(false);
            
            await Serializer.LoadAsync(stream, Track, ui).ConfigureAwait(false);
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