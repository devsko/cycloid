using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using cycloid.Serizalization;
using Windows.Storage;
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

        Track = new Track(file);
        Stopwatch watch = Stopwatch.StartNew();
        await Serializer.LoadAsync(Track);
        Status = $"{Track.Name} opened ({watch.ElapsedMilliseconds} ms)";
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
            }
            catch (OperationCanceledException) when (_saveTrackCts.IsCancellationRequested)
            { }
        }
    }
}