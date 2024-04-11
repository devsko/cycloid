using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using cycloid.Serizalization;
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
    private CancellationTokenSource _saveTrackCts;
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
        if (!Program.RegisterForFile(file, out _))
        {
            if (!dontShowDialog)
            {
                await ShowFileAlreadyOpenAsync(file);
            }
        }
        else
        {
            StorageApplicationPermissions.FutureAccessList.AddOrReplace("LastTrack", file);
            await LoadTrackFileAsync(file);
        }
    }

    [RelayCommand]
    public async Task NewTrackAsync()
    {
        FileSavePicker picker = new()
        {
            SuggestedFileName = "New Track",
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeChoices.Add("Track", [".track"]);
        StorageFile file = await picker.PickSaveFileAsync();

        if (file is null)
        {
            return;
        }

        if (!Program.RegisterForFile(file, out _))
        {
            await ShowFileAlreadyOpenAsync(file);
        }
        else
        {
            if (StorageApplicationPermissions.FutureAccessList.ContainsItem("LastTrack"))
            {
                StorageApplicationPermissions.FutureAccessList.Remove("LastTrack");
            }

            Track = new Track(file);

            StrongReferenceMessenger.Default.Send(new TrackComplete(true));

            Status = $"{Track.Name} created";
        }
    }

    [RelayCommand]
    public async Task SaveTrackAsync()
    {
        if (Track is not null)
        {
            _saveTrackCts?.Cancel();
            _saveTrackCts = new CancellationTokenSource();
            try
            {
                await _saveTrackSemaphore.WaitAsync(_saveTrackCts.Token);

                Stopwatch watch = Stopwatch.StartNew();

                await Serializer.SaveAsync(Track, _saveTrackCts.Token);

                Status = $"{Track.Name} saved ({watch.ElapsedMilliseconds} ms) {++_saveCounter}";

                StorageApplicationPermissions.FutureAccessList.AddOrReplace("LastTrack", Track.File);
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
        Track = new Track(file);

        Stopwatch watch = Stopwatch.StartNew();

        await Track.LoadAsync();

        StrongReferenceMessenger.Default.Send(new TrackComplete(false));

        Status = $"{Track.Name} opened ({watch.ElapsedMilliseconds} ms)";
    }

    private async Task ShowFileAlreadyOpenAsync(IStorageFile file)
    {
        await new ContentDialog
        {
            Title = "Cannot open file",
            Content = $"{file.Name} is already open.",
            CloseButtonText = "Close",
        }.ShowAsync();
    }
}