# TODO cycloid

dotnet build
DesktopBridge setzt immer erstmal TargetPlatform UAP für den Restore 
- wenn ein referenziertes Package auch UAP unterstützt wird falsch restored
- wenn nicht, kommt eine Warnung 

## Links
- [Bing maps styling](https://learn.microsoft.com/en-us/bingmaps/styling/map-style-sheet-entry-properties)
- [Bing maps tiling](https://learn.microsoft.com/en-us/bingmaps/articles/bing-maps-tile-system)
- [App elevation sample](https://stefanwick.com/2018/10/07/app-elevation-samples-part-3/)

## Bugs
- Surface verschoben (Holzbrücke in CH bei km 1402)
- Profile zeichnet nach oben bis in die Karte beim Zeichnen der Route
- Fehler auf Profile wenn Kalkulation läuft (war nach Löschen von Selection)
- Track mit Schleifen, CompareSession nimmt immer den 1. Point des Tracks (40f12f68)
## Perf
- Profile MaxElevation in EnsureTrack oder aus den MaxAltitudes der Segments in PointColection - auf jeden Fall nicht nochmal Enumerate()
## Feature
- Splits im Profile
	- Zu kleine Gaps automatisch löschen
	- Nicht im Edit-Mode
	- Anzeigen auf der Map
	- Serialisieren
- Train Mode
	- Tempo einstellbar
	- Aktuelle Werte/Section anzeigen
	- Entfernung zu den nächsten POIs nach Kategorie
	- Pausenanteil pro Tag / Schlafstopp einstellbar
	- Planung bis Uhrzeit
	- CurrentPoint erzwingen (kann nicht durch Click in die Landschaft entfernt werden)
	- HoverPoint auf dem Track zulassen wenn IsPlaying false
	- 10Minute Skip immer erlauben (Weil immer CurrentPoint vorhanden)
- Export Tab
- (Höhenakkumulator zwischen Segmenten) Neuer WayPoint auf dadurch unveränderter Route -> warum ändern sich die Total Values
- Serialisierte Points behalten wenn nicht ver ndert
- WayPoints müssen eindeutig sein?
- Undo (Historie auch speichern) https://github.com/Doraku/DefaultUnDo
- Sections u Splits im ProfileControl anzeigen
