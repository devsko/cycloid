using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using cycloid.Info;
using Windows.UI.Xaml.Controls.Maps;

namespace cycloid;

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

    [ObservableProperty]
    private MapStyleAndColor _mapStyleAndColor = MapStyleAndColors[0];

    [ObservableProperty]
    private bool _heatmapVisible;

    [ObservableProperty]
    private bool _trackVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PoisVisible))]
    private bool _poisShouldVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InfoVisible))]
    private bool _infoShouldVisible = true;

    [ObservableProperty]
    private bool _poisAreLoading;

    [ObservableProperty]
    private bool _infoIsLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InfoEnabled))]
    [NotifyPropertyChangedFor(nameof(InfoVisible))]
    private double _mapZoomLevel;

    private readonly Dictionary<InfoCategory, bool> _poisCategories = InfoCategory.All.ToDictionary(category => category, _ => true);
    private readonly Dictionary<InfoCategory, bool> _infoCategories = InfoCategory.All.ToDictionary(category => category, _ => true);

    public event Action<bool, bool> InfoVisibleChanged;
    public event Action<bool, bool> PoisVisibleChanged;
    public event Action<InfoCategory, bool> InfoCategoryVisibleChanged;
    public event Action<InfoCategory, bool> PoisCategoryVisibleChanged;

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

    partial void OnPoisShouldVisibleChanged(bool oldValue, bool newValue)
    {
        PoisVisibleChanged?.Invoke(oldValue, newValue);
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

    partial void OnInfoShouldVisibleChanged(bool oldValue, bool newValue)
    {
        InfoVisibleChanged?.Invoke(oldValue, newValue);
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
        (pois ? PoisCategoryVisibleChanged : InfoCategoryVisibleChanged)?.Invoke(category, value);
    }

    public bool GetInfoCategoryVisible(bool pois, InfoCategory category)
    {
        return (pois ? _poisCategories : _infoCategories)[category];
    }
}
