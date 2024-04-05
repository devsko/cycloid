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
        new("OSM", MapStyleSheet.Combine([StyleSheet.Empty, StyleSheet.Extension]), Osm: true),
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
    private bool _infoIsLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InfoEnabled))]
    [NotifyPropertyChangedFor(nameof(InfoVisible))]
    private double _mapZoomLevel;

    private readonly Dictionary<InfoCategory, bool> _infoCategories = InfoCategory.All.ToDictionary(category => category, _ => true);

    public event Action<bool, bool> InfoVisibleChanged;
    public event Action<InfoCategory, bool> InfoCategoryVisibleChanged;

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

    public void SetInfoCategoryVisible(InfoCategory category, bool value)
    {
        if (category is null)
        {
            foreach (InfoCategory c in InfoCategory.All)
            {
                _infoCategories[c] = value;
            }
        }
        else
        {
            _infoCategories[category] = value;
        }
        InfoCategoryVisibleChanged?.Invoke(category, value);
    }

    public bool GetInfoCategoryVisible(InfoCategory category)
    {
        return _infoCategories[category];
    }
}
