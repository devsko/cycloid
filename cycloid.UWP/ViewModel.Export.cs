using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Windows.ApplicationModel;

namespace cycloid;

partial class ViewModel
{
    [RelayCommand(CanExecute = nameof(CanExportWahoo))]
    public async Task ExportWahooAsync()
    {
        await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppWithArgumentsAsync(Track.File.Path);
    }

    private bool CanExportWahoo()
    {
        return HasTrack;
    }
}