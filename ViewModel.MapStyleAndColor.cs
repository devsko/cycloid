using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;
using Windows.UI.Xaml.Controls.Maps;

namespace cycloid;

public readonly record struct MapStyleAndColor(string Name, MapStyleSheet StyleSheet, bool Osm = false);

partial class ViewModel
{
    public static readonly MapStyleAndColor[] MapStyleAndColors = 
    [
        new("Road (Dark)", MapStyleSheet.Combine([MapStyleSheet.RoadDark(), StyleSheet.Extension])),
        new("Road (Light)", MapStyleSheet.Combine([MapStyleSheet.RoadLight(), StyleSheet.Extension])),
        new("Aerial", MapStyleSheet.Combine([MapStyleSheet.Aerial(), StyleSheet.Extension])),
        new("Aerial with roads", MapStyleSheet.Combine([MapStyleSheet.AerialWithOverlay(), StyleSheet.Extension])),
        new("OSM", MapStyleSheet.Combine([StyleSheet.Empty, StyleSheet.Extension, StyleSheet.InterestingPoints]), Osm: true),
    ];

    [ObservableProperty]
    private MapStyleAndColor _mapStyleAndColor = MapStyleAndColors.First(s => s.Name == "Aerial");
}
