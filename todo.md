# TODO cycloid

- BUG Track mit Schleifen, Exception bei ChangeSession Start
- Auf Train Seite CurrentPoint erzwingen (kann nicht durch Click in die Landschaft entfernt werden)
- Auf Train Seite HoverPoint auf dem Track zulassen wenn IsPlaying false
- Auf Train Seite 10Minute Skip immer erlaubens

dotnet build
DesktopBridge setzt immer erstmal TargetPlatform UAP für den Restore 
- wenn ein referenziertes Package auch UAP unterstützt wird falsch restored
- wenn nicht, kommt eine Warnung 

- Review ChangeSession ändern von Diff: e928bfe REVIEW!!

## Links
- [Bing maps styling](https://learn.microsoft.com/en-us/bingmaps/styling/map-style-sheet-entry-properties)
- [Bing maps tiling](https://learn.microsoft.com/en-us/bingmaps/articles/bing-maps-tile-system)
- [App elevation sample](https://stefanwick.com/2018/10/07/app-elevation-samples-part-3/)

- CT Tooling https://github.com/CommunityToolkit/Tooling-Windows-Submodule/tree/uwp-net8-windows
- Build CT NuGet packages locally `.\tooling\Build-Toolkit-Components.ps1 -MultiTargets uwp -winui 2 -Components AppServices -PreviewVersion local -NupkgOutput ./some/output/path -Release`

ProfileControl Surface
CurrentItem in Liste der Diffs

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
- Serialisierte Points behalten wenn nicht ver ndert
- WayPoints müssen eindeutig sein?
- Undo (Historie auch speichern) https://github.com/Doraku/DefaultUnDo
- Splash-Screen
- Camera als ViewModel? oder in Map integriert
- Sections u Splits im ProfileControl anzeigen
