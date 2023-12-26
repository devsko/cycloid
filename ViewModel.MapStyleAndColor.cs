using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.UI.Xaml.Controls.Maps;

namespace cycloid;

public readonly record struct MapStyleAndColor(string Name, MapStyle Style, MapColorScheme Color, bool Osm = false);

partial class ViewModel
{
    public static readonly MapStyleAndColor[] MapStyleAndColors =
    [
        new("None", MapStyle.None, MapColorScheme.Light),
        new("Road (Dark)", MapStyle.Road, MapColorScheme.Dark),
        new("Road (Light)", MapStyle.Road, MapColorScheme.Light),
        new("Aerial", MapStyle.Aerial, MapColorScheme.Light),
        new("Aerial with roads", MapStyle.AerialWithRoads, MapColorScheme.Light),
        new("OSM", MapStyle.None, MapColorScheme.Light, Osm: true),
        new("3D", MapStyle.Aerial3D, MapColorScheme.Light),
        new("3D with roads", MapStyle.Aerial3DWithRoads, MapColorScheme.Light),
    ];

    [ObservableProperty]
    private MapStyleAndColor _mapStyleAndColor = MapStyleAndColors.First(s => s.Style == MapStyle.AerialWithRoads);
}
