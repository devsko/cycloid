using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace cycloid;

partial class ViewModel
{
    private bool _heatmapVisible;
    public bool HeatmapVisible
    {
        get => _heatmapVisible;
        private set => SetProperty(ref _heatmapVisible, value);
    }

    [RelayCommand]
    public async Task ToggleHeatmapVisibleAsync()
    {
        if (HeatmapVisible)
        {
            HeatmapVisible = false;
        }
        else
        {
            HeatmapVisible = await Strava.InitializeHeatmapAsync();
            // Notify property changed again to convinvce the toggle button
            OnPropertyChanged(nameof(HeatmapVisible));
        }
    }
}