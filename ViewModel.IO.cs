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
    private readonly SemaphoreSlim _saveTrackSemaphore = new(1);
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
        await OpenTrackFileAsync(file);
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
        InitializePointsOfInterest();

        TrackIsInitialized = true;

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

        if (StorageApplicationPermissions.FutureAccessList.ContainsItem("LastTrack"))
        {
            StorageApplicationPermissions.FutureAccessList.Remove("LastTrack");
        }

        Track = new Track(file);
        InitializePointsOfInterest();
        
        TrackIsInitialized = true;

        Status = $"{Track.Name} created";
    }

    private void InitializePointsOfInterest()
    {
        foreach (PointOfInterest pointOfInterest in Track.PointsOfInterest)
        {
            pointOfInterest.PropertyChanged += PointOfInterest_PropertyChanged;
        }
        CreateAllOnTrackPoints();
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

                Status = $"{Track.Name} saved ({watch.ElapsedMilliseconds} ms)";

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
}