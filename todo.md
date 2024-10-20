# TODO cycloid

## Modern UWP

I am currently trying to upgrade a pretty large project to modern UWP. It’s a bit tedious because many UWP packages can’t be used as they are and need to be adjusted locally. Right now i need local builds amongst others of
- Microsoft.UI.Xaml
- Microsoft.Xaml.Behaviors.Uwp.Managed
- CommunityToolkit Windows and Labs-Windows

So far i recognized several issues (in no particular order)
- a reference to Microsoft.VisualStudio.Threading pulls in WPF and WinForms (it does this also under WinUI but without errors). In modern UWP this results in XamlCompiler error WMC1006: Cannot resolve Assembly or Windows Metadata file 'Type universe cannot resolve assembly: System.Windows.Forms, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089.'
- Several IL2059 warnings for nullable enums (i.e. here typeof(global::System.Nullable<global::CommunityToolkit.WinUI.Animations.EasingType>)
- InvalidCastExceptions when passing IEnumerables of WinRT types to WinRT
  - using Windows.Devices.Geolocation;
  Geopath path = new([new BasicGeoposition(0, 0, 0)]);
  - using Windows.UI.Xaml.Controls.Maps;
  MapStyleSheet.Combine([
    MapStyleSheet.Aerial(), 
    MapStyleSheet.ParseFromJson("{\"version\": \"1.*\"}")]);


## Dependencies
- BehaviorsSDKManaged.sln
  - Release|AnyCPU Build
  - scripts>nuget pack Microsoft.Xaml.Behaviors.Uwp.Managed.nuspec -version 3.0.241018-local
  - scripts>nuget push Microsoft.Xaml.Behaviors.Uwp.Managed.3.0.241018-local.nupkg -source D:\packages\NuGet\feed
- CommunityToolkit
  - CommunityToolkit> Windows\tooling\Build-Toolkit-Components.ps1 -MultiTargets uwp -PreviewVersion local -NupkgOutput d:\repos\communitytoolkit\packages -Release
  - CommunityToolkit> nuget push .\packages\CommunityToolkit.Uwp.*.nupkg -source d:\packages\NuGet\feed
  - CommunityToolkit> Labs-Windows\tooling\Build-Toolkit-Components.ps1 -MultiTargets uwp -PreviewVersion local -NupkgOutput d:\repos\communitytoolkit\packages -Release
  - CommunityToolkit> nuget push .\packages\CommunityToolkit.Labs.*.nupkg -source d:\packages\NuGet\feed

## Links
- [Bing maps styling](https://learn.microsoft.com/en-us/bingmaps/styling/map-style-sheet-entry-properties)
- [Bing maps tiling](https://learn.microsoft.com/en-us/bingmaps/articles/bing-maps-tile-system)
- [App elevation sample](https://stefanwick.com/2018/10/07/app-elevation-samples-part-3/)

- CT Tooling https://github.com/CommunityToolkit/Tooling-Windows-Submodule/tree/uwp-net8-windows
- Build CT NuGet packages locally `.\tooling\Build-Toolkit-Components.ps1 -MultiTargets uwp -winui 2 -Components AppServices -PreviewVersion local -NupkgOutput ./some/output/path -Release`
## Bugs
- Surface verschoben (Holzbrücke in CH bei km 1402)
- Profile zeichnet nach oben bis in die Karte beim Zeichnen der Route
- Fehler auf Profile wenn Kalkulation läuft (war nach Löschen von Selection)
## Perf
- Profile MaxElevation in EnsureTrack oder aus den MaxAltitudes der Segments in PointColection - auf jeden Fall nicht nochmal Enumerate()
## Feature
- StreetView
- Train Mode
- Start-Screen (ersetzt Öffnen/Neu AppButton)
  - Checkbox "in neuem Fenster"
  - Neu
  - Öffnen
  - Zuletzt
- (Höhenakkumulator zwischen Segmenten) Neuer WayPoint auf dadurch unveränderter Route -> warum ändern sich die Total Values
- Serialisierte Points behalten wenn nicht verändert
- WayPoints müssen eindeutig sein?
- Undo (Historie auch speichern) https://github.com/Doraku/DefaultUnDo
- Splash-Screen
- Camera als ViewModel? oder in Map integriert
- Sections u Splits im ProfileControl anzeigen
