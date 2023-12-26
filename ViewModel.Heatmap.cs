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

    public string HeatmapUri => App.Current.Strava.HeatmapUri;

    [RelayCommand]
    public async Task ToggleHeatmapVisibleAsync()
    {
        if (HeatmapVisible)
        {
            HeatmapVisible = false;
        }
        else
        {
            HeatmapVisible = await App.Current.Strava.InitializeHeatmapAsync();
            // Notify property changed again to convinvce the toggle button
            OnPropertyChanged(nameof(HeatmapVisible));
        }
    }
}