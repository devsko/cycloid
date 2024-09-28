# TODO cycloid

## Links
- [Bing maps styling](https://learn.microsoft.com/en-us/bingmaps/styling/map-style-sheet-entry-properties)
- [Bing maps tiling](https://learn.microsoft.com/en-us/bingmaps/articles/bing-maps-tile-system)
- [App elevation sample](https://stefanwick.com/2018/10/07/app-elevation-samples-part-3/)
## Bugs
- Surface verschoben (Holzbr�cke in CH bei km 1402)
- Profile zeichnet nach oben bis in die Karte beim Zeichnen der Route
- Fehler auf Profile wenn Kalkulation l�uft (war nach L�schen von Selection)
## Perf
- Profile MaxElevation in EnsureTrack oder aus den MaxAltitudes der Segments in PointColection - auf jeden Fall nicht nochmal Enumerate()
## Feature
- StreetView
- Train Mode
- Start-Screen (ersetzt �ffnen/Neu AppButton)
  - Checkbox "in neuem Fenster"
  - Neu
  - �ffnen
  - Zuletzt
- (H�henakkumulator zwischen Segmenten) Neuer WayPoint auf dadurch unver�nderter Route -> warum �ndern sich die Total Values
- Serialisierte Points behalten wenn nicht ver�ndert
- WayPoints m�ssen eindeutig sein?
- Undo (Historie auch speichern)
- Splash-Screen
- Camera als ViewModel? oder in Map integriert
- Sections u Splits im ProfileControl anzeigen
