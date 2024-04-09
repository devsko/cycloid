using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using cycloid.Info;
using Windows.UI.Xaml.Controls.Maps;

namespace cycloid;

public class PoisVisibleChanged(object sender, bool oldValue, bool newValue) : PropertyChangedMessage<bool>(sender, null, oldValue, newValue);

public class InfoVisibleChanged(object sender, bool oldValue, bool newValue) : PropertyChangedMessage<bool>(sender, null, oldValue, newValue);

public readonly record struct MapStyleAndColor(string Name, MapStyleSheet StyleSheet, bool Osm = false);

partial class ViewModel
{
    public static readonly MapStyleAndColor[] MapStyleAndColors =
    [
        new("Aerial", MapStyleSheet.Combine([MapStyleSheet.Aerial(), StyleSheet.Extension])),
        new("Aerial with roads", MapStyleSheet.Combine([MapStyleSheet.AerialWithOverlay(), StyleSheet.Extension])),
        new("OSM", MapStyleSheet.Combine([MapStyleSheet.RoadLight(), StyleSheet.Empty, StyleSheet.Extension]), Osm: true),
        new("Road (Dark)", MapStyleSheet.Combine([MapStyleSheet.RoadDark(), StyleSheet.Extension])),
        new("Road (Light)", MapStyleSheet.Combine([MapStyleSheet.RoadLight(), StyleSheet.Extension])),
    ];

    public const double MinInfoZoomLevel = 13;

    private MapStyleAndColor _mapStyleAndColor = MapStyleAndColors[0];
    public MapStyleAndColor MapStyleAndColor
    {
        get => _mapStyleAndColor;
        set => SetProperty(ref _mapStyleAndColor, value);
    }

    private bool _heatmapVisible;
    public bool HeatmapVisible
    {
        get => _heatmapVisible;
        set => SetProperty(ref _heatmapVisible, value);
    }

    private bool _trackVisible = true;
    public bool TrackVisible
    {
        get => _trackVisible;
        set => SetProperty(ref _trackVisible, value);
    }

    private bool _poisShouldVisible = true;
    public bool PoisShouldVisible
    {
        get => _poisShouldVisible;
        set
        {
            bool oldValue = _poisShouldVisible;
            if (SetProperty(ref _poisShouldVisible, value))
            {
                OnPropertyChanged(nameof(PoisVisible));

                StrongReferenceMessenger.Default.Send(new PoisVisibleChanged(this, oldValue, value));
            }
        }
    }

    private bool _infoShouldVisible = true;
    public bool InfoShouldVisible
    {
        get => _infoShouldVisible;
        set
        {
            bool oldValue = _infoShouldVisible;
            if (SetProperty(ref _infoShouldVisible, value))
            {
                OnPropertyChanged(nameof(InfoVisible));

                StrongReferenceMessenger.Default.Send(new InfoVisibleChanged(this, oldValue, value));
            }
        }
    }

    private bool _poisAreLoading;
    public bool PoisAreLoading
    {
        get => _poisAreLoading;
        set => SetProperty(ref _poisAreLoading, value);
    }

    private bool _infoIsLoading;
    public bool InfoIsLoading
    {
        get => _infoIsLoading;
        set => SetProperty(ref _infoIsLoading, value);
    }

    private double _mapZoomLevel;
    public double MapZoomLevel
    {
        get => _mapZoomLevel;
        set
        {
            if (SetProperty(ref _mapZoomLevel, value))
            {
                OnPropertyChanged(nameof(InfoEnabled));
                OnPropertyChanged(nameof(InfoVisible));
            }
        }
    }

    private readonly Dictionary<InfoCategory, bool> _poisCategories = InfoCategory.All.ToDictionary(category => category, _ => true);
    private readonly Dictionary<InfoCategory, bool> _infoCategories = InfoCategory.All.ToDictionary(category => category, _ => true);

    public bool PoisEnabled => Mode != Modes.Edit;

    public bool PoisVisible
    {
        get => PoisShouldVisible && PoisEnabled;
        set
        {
            if (PoisEnabled)
            {
                PoisShouldVisible = value;
            }
            // Notify property changed again to convinvce the toggle button
            OnPropertyChanged(nameof(PoisVisible));
        }
    }

    public bool InfoEnabled => MapZoomLevel >= MinInfoZoomLevel;

    public bool InfoVisible
    {
        get => InfoShouldVisible && InfoEnabled;
        set
        {
            if (InfoEnabled)
            {
                InfoShouldVisible = value;
            }
            // Notify property changed again to convinvce the toggle button
            OnPropertyChanged(nameof(InfoVisible));
        }
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
            HeatmapVisible = await Strava.InitializeHeatmapAsync(clearCookies: false);
            // Notify property changed again to convinvce the toggle button
            OnPropertyChanged(nameof(HeatmapVisible));
        }
    }

    public void SetInfoCategoryVisible(bool pois, InfoCategory category, bool value)
    {
        if (category is null)
        {
            foreach (InfoCategory c in InfoCategory.All)
            {
                (pois ? _poisCategories : _infoCategories)[c] = value;
            }
        }
        else
        {
            (pois ? _poisCategories : _infoCategories)[category] = value;
        }

        StrongReferenceMessenger.Default.Send(new InfoCategoryVisibleChanged(this, pois, category, !value, value));
    }

    public bool GetInfoCategoryVisible(bool pois, InfoCategory category)
    {
        return (pois ? _poisCategories : _infoCategories)[category];
    }
}
