# TODO cycloid

## Links
- [Bing maps styling](https://learn.microsoft.com/en-us/bingmaps/styling/map-style-sheet-entry-properties)
- [Bing maps tiling](https://learn.microsoft.com/en-us/bingmaps/articles/bing-maps-tile-system)
- [App elevation sample](https://stefanwick.com/2018/10/07/app-elevation-samples-part-3/)
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
- Undo (Historie auch speichern)
- Splash-Screen
- Camera als ViewModel? oder in Map integriert
- Sections u Splits im ProfileControl anzeigen
