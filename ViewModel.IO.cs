using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using cycloid.Serizalization;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;

namespace cycloid;

partial class ViewModel
{
    private CancellationTokenSource _saveTrackCts;

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

        StorageApplicationPermissions.FutureAccessList.AddOrReplace("LastTrack", file);
        await  OpenTrackFileAsync(file);
    }

    public async Task OpenLastTrackAsync()
    {
        if (StorageApplicationPermissions.FutureAccessList.ContainsItem("LastTrack"))
        {
            try
            {
                StorageFile file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync("LastTrack");
                await OpenTrackFileAsync(file);
            }
            catch (FileNotFoundException)
            {
                StorageApplicationPermissions.FutureAccessList.Remove("LastTrack");
            }
        }
    }

    private async Task OpenTrackFileAsync(StorageFile file)
    {
        Track = new Track(file);

        Stopwatch watch = Stopwatch.StartNew();
        await Track.LoadAsync();
        Status = $"{Track.Name} opened ({watch.ElapsedMilliseconds} ms)";
    }

    [RelayCommand]
    private async Task NewTrackAsync()
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

        StorageApplicationPermissions.FutureAccessList.Remove("LastTrack");

        Track = new Track(file);
        Status = $"{Track.Name} created";
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
                Stopwatch watch = Stopwatch.StartNew();
                await Serializer.SaveAsync(Track, _saveTrackCts.Token);
                Status = $"{Track.Name} saved ({watch.ElapsedMilliseconds} ms)";

                StorageApplicationPermissions.FutureAccessList.AddOrReplace("LastTrack", Track.File);
            }
            catch (OperationCanceledException)
            { }
        }
    }
}