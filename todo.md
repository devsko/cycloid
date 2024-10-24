# TODO cycloid

dotnet build
DesktopBridge setzt immer erstmal TargetPlatform UAP für den Restore 
- wenn ein referenziertes Package auch UAP unterstützt wird falsch restored
- wenn nicht, kommt eine Warnung 


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
- Surface verschoben (Holzbr cke in CH bei km 1402)
- Profile zeichnet nach oben bis in die Karte beim Zeichnen der Route
- Fehler auf Profile wenn Kalkulation l uft (war nach L schen von Selection)
## Perf
- Profile MaxElevation in EnsureTrack oder aus den MaxAltitudes der Segments in PointColection - auf jeden Fall nicht nochmal Enumerate()
## Feature
- StreetView
- Train Mode
- Start-Screen (ersetzt  ffnen/Neu AppButton)
  - Checkbox "in neuem Fenster"
  - Neu
  -  ffnen
  - Zuletzt
- (H henakkumulator zwischen Segmenten) Neuer WayPoint auf dadurch unver nderter Route -> warum  ndern sich die Total Values
- Serialisierte Points behalten wenn nicht ver ndert
- WayPoints m ssen eindeutig sein?
- Undo (Historie auch speichern) https://github.com/Doraku/DefaultUnDo
- Splash-Screen
- Camera als ViewModel? oder in Map integriert
- Sections u Splits im ProfileControl anzeigen
