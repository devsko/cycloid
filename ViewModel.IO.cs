using CommunityToolkit.Mvvm.Input;
using cycloid.Serizalization;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace cycloid;

partial class ViewModel
{
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
        await Serializer.LoadAsync(Track);
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
    }

    [RelayCommand]
    public async Task SaveTrackAsync()
    {
        if (Track is not null)
        {
            await Serializer.SaveAsync(Track);
        }
    }
}