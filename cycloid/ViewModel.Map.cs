using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using cycloid.Info;
using Windows.UI.Xaml.Controls.Maps;
using WinRT;

namespace cycloid;

public class PoisVisibleChanged(bool value) : ValueChangedMessage<bool>(value);

public class InfoVisibleChanged(bool value) : ValueChangedMessage<bool>(value);

[GeneratedBindableCustomProperty([nameof(Name)], null)]
public partial record MapStyleAndColor(string Name, MapStyleSheet StyleSheet, bool Osm = false);

partial class ViewModel
{
    public static readonly MapStyleAndColor[] MapStyleAndColors =
    [
        new("Aerial", MapStyleSheet.Combine(new MapStyleSheet[] { MapStyleSheet.Aerial(), StyleSheet.Extension })),
        new("Aerial with roads", MapStyleSheet.Combine(new MapStyleSheet[] { MapStyleSheet.AerialWithOverlay(), StyleSheet.Extension })),
        new("OSM", MapStyleSheet.Combine(new MapStyleSheet[] { MapStyleSheet.RoadLight(), StyleSheet.Empty, StyleSheet.Extension }), Osm: true),
        new("Road (Dark)", MapStyleSheet.Combine(new MapStyleSheet[] { MapStyleSheet.RoadDark(), StyleSheet.Extension })),
        new("Road (Light)", MapStyleSheet.Combine(new MapStyleSheet[] { MapStyleSheet.RoadLight(), StyleSheet.Extension })),
    ];

    public const double MinInfoZoomLevel = 13;

    private readonly Dictionary<InfoCategory, bool> _poisCategories = InfoCategory.All.ToDictionary(category => category, _ => true);
    private readonly Dictionary<InfoCategory, bool> _infoCategories = InfoCategory.All.ToDictionary(category => category, _ => true);

    [ObservableProperty]
    public partial MapStyleAndColor MapStyleAndColor { get; set; } = MapStyleAndColors[0];

    [ObservableProperty]
    public partial bool HeatmapVisible { get; set; }

    [ObservableProperty]
    public partial bool TrackVisible { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InfoEnabled))]
    [NotifyPropertyChangedFor(nameof(InfoVisible))]
    public partial double MapZoomLevel { get; set; }

    [ObservableProperty]
    public partial bool PoisAreLoading { get; set; }

    [ObservableProperty]
    public partial bool InfosAreLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PoisVisible))]
    public partial bool PoisShouldVisible { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InfoVisible))]
    public partial bool InfoShouldVisible { get; set; } = true;

    partial void OnPoisShouldVisibleChanged(bool value)
    {
        StrongReferenceMessenger.Default.Send(new PoisVisibleChanged(value));
    }

    partial void OnInfoShouldVisibleChanged(bool value)
    {
        StrongReferenceMessenger.Default.Send(new InfoVisibleChanged(value));
    }

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
