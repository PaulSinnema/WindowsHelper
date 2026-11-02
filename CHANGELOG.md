# Changelog

Alle noemenswaardige wijzigingen in WindowsHelper.
Versienummer staat in `Directory.Build.props` en wordt bij elke iteratie opgehoogd.

## [1.0.15] – 2026-11-02
### Verwijderd
- Diagnose uit 1.0.14 (uiAccess-log bij start, log bij geweigerde z-volgorde). Oorzaak was gevonden: de Debug-versie heeft geen `uiAccess`; start Visual Studio als administrator om administrator-vensters te testen.

## [1.0.14] – 2026-11-02
### Toegevoegd
- Log bij start: of `uiAccess` actief is, of het proces elevated draait en vanaf welk pad.
- Log als Windows een z-volgorde-wijziging weigert (onderzoek naar overlays achter administrator-vensters zoals Device Manager).

## [1.0.13] – 2026-11-02
### Opgelost
- Werkbalk verscheen niet bij nieuwe dialoogvensters (bijv. Region): de focusmelding kwam terwijl het venster nog onzichtbaar was. De 200 ms-controle haalt het actieve venster nu in.

## [1.0.12] – 2026-11-02
### Verwijderd
- Extra sneltoets `Ctrl+Alt+Shift+S` (uit 1.0.11) weer verwijderd; alleen `Ctrl+Alt+S`.

## [1.0.11] – 2026-11-02
### Toegevoegd
- Diagnose (`Ctrl+Alt+Shift+D`) logt ook het voorgrondvenster en de status van de werkbalk.

## [1.0.10] – 2026-11-02
Teruggezet naar de stabiele basis van 0.1.10. De experimenten uit 1.0.1–1.0.9 zijn teruggedraaid
(donker placeholder-vlak bij nieuwe vensters, voorbereide overlays, opname-item op achtergrondthread,
latere aanpassingen aan de owner-koppeling): die gaven onzichtbare dialogen, zwarte vlakken en vertraging.

### Toegevoegd
- Sneltoets `Ctrl+Alt+S`: instellingen van het actieve programma openen.
- Sneltoetsenoverzicht in het algemene instellingenvenster; tooltips van de werkbalk tonen de sneltoetsen.
- README.md en CHANGELOG.md.

### Gewijzigd
- Versienummering naar 1.0.x.
- Werkbalk staat in het midden van de titelbalk, zodat eigen iconen van programma's (Loxone, Chrome) vrij blijven. Verborgen bij vensters smaller dan ~370 px.

## [0.1.10] – 2026-11-02
Basisversie.

### Functies
- Dark mode voor elk venster via schermopname (Windows.Graphics.Capture) + GPU-shaders (Direct3D 11).
- "Smart invert": helderheid omkeren met behoud van kleurtint, met gamma zodat middengrijs donker wordt; donkere gebieden blijven ongemoeid.
- Afbeeldingsherkenning: alleen grote beeldgebieden (≥ ~120 px) blijven origineel; de effen achtergrond eromheen wordt toch omgekeerd.
- Instellingen per programma (exe-naam), werkbalk in de titelbalk, tray-icoon met algemene instellingen.
- Sneltoetsen `Ctrl+Alt+D` (dark mode aan/uit) en `Ctrl+Alt+Shift+D` (diagnose).
- Ook zwevende panelen, keuzelijsten, menu's en tooltips worden donker.
- Overlay en werkbalk zijn owner-gekoppeld aan het doelvenster (geen flits bij focuswissel).
- Vangnet dat verweesde overlays opruimt; overlay verdwijnt bij verbergen, minimaliseren of sluiten.
- Eigen UI volledig in dark mode, grote letters, hoog contrast.
- MSI-installer (WiX 6) met zelfondertekend certificaat en `uiAccess`.
