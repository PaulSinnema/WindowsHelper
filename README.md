# WindowsHelper

Dwingt dark mode af op elk venster in Windows, per programma instelbaar.
Bedoeld voor betere leesbaarheid (bijv. bij maculadegeneratie): witte achtergronden worden donker, tekst licht, afbeeldingen blijven origineel.

## Functies
- Dark mode per programma aan/uit en instelbaar (achtergrond, teksthelderheid, afbeeldingen, donkere delen).
- Optionele werkbalk in het midden van de titelbalk van het actieve venster: maan = aan/uit, tandwiel = instellingen voor dat programma.
- Tray-icoon: alles aan/uit, werkbalk, starten met Windows, algemene instellingen, lijst van programma's.
- Sneltoetsen (zie hieronder) – bediening zonder iets in beeld.
- Werkt ook boven vensters die als administrator draaien (`uiAccess`).

## Sneltoetsen
Alle sneltoetsen werken globaal en gelden voor het **actieve programma** (het venster met de focus).

| Sneltoets | Actie |
|---|---|
| `Ctrl+Alt+D` | Dark mode aan/uit voor het actieve programma. De keuze wordt per programma bewaard. |
| `Ctrl+Alt+S` | Instellingenvenster van het actieve programma openen (rechtsboven in dat venster). Sluiten met `Esc`. |
| `Ctrl+Alt+Shift+D` | Diagnose: schrijft `dump-source.png`, `dump-result.png` en venstergeometrie van alle overlays naar `%APPDATA%\WindowsHelper`. Geeft geen zichtbare melding. |

In het instellingenvenster:
- `Tab` / `Shift+Tab` – naar volgende/vorige instelling
- `Spatie` – vinkje aan/uit
- Pijltjes `←` `→` – schuifregelaar per 1 %, `PageUp` / `PageDown` per 10 %; ook met het muiswiel
- `Esc` – sluiten

Is een sneltoets al in gebruik door een ander programma, dan meldt het log `Hotkey … niet beschikbaar`.
Het overzicht staat ook in het algemene instellingenvenster (tray-icoon → *Instellingen…*).

## Werkbalk in de titelbalk
Optioneel (tray-icoon → *Werkbalk in titelbalk*). Staat in het midden van de titelbalk van het actieve venster,
zodat eigen iconen van programma's met een zelfgetekende titelbalk (Loxone Config, Chrome, VS Code) vrij blijven.
Wordt verborgen bij vensters smaller dan ~370 px. Wie liever alleen met sneltoetsen werkt, zet de werkbalk uit.

## Hoe het werkt
1. **Detectie** – `SetWinEventHook` (out-of-context, geen DLL-injectie) meldt tonen, verplaatsen, focus, minimaliseren en sluiten van vensters.
2. **Opname** – Windows.Graphics.Capture neemt het doelvenster op (ook als het bedekt is).
3. **Verwerking** – Direct3D 11 compute- en pixelshaders (via Vortice):
   - classificatie per tegel van 8 px (foto/kleur/effen achtergrond),
   - alleen grote beeldgebieden worden beschermd, de achtergrondkleur eromheen wordt alsnog omgekeerd,
   - "smart invert": helderheid omkeren met behoud van tint; donkere gebieden blijven ongemoeid.
4. **Weergave** – een klik-doorlatende overlay (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE`) ligt precies boven het venster.
   De overlay verschijnt pas als het eerste donkere beeld klaar is, en is owner-gekoppeld aan het doelvenster zodat hij bij focuswissel meegaat.

## Projectstructuur
```
WindowsHelper.sln
Directory.Build.props        Versienummer (ophogen bij elke iteratie)
README.md / CHANGELOG.md
WindowsHelper/               WinForms tray-app (.NET 10, x64)
  Core/                      WindowTracker, DarkModeManager, DarkOverlay
  Rendering/                 GpuContext, CaptureRenderer, CaptureHelper, Shaders (HLSL)
  Interop/                   P/Invoke, vensterinfo, z-order
  Settings/                  Instellingen (JSON), autostart
  UI/                        Thema, werkbalk, tray, formulieren, slider, sneltoetsen
Installer/                   WiX 6 MSI (Package.wxs)
tools/Sign.ps1               Certificaat aanmaken + exe ondertekenen
```

## Vereisten
- Windows 10 2004+ / Windows 11, x64
- .NET 10 Desktop Runtime
- Ontwikkelen: Visual Studio 2026 (18.x) met .NET 10 SDK

## Bouwen
- **Debug** (`Debug|x64`): draait direct vanuit Visual Studio, zonder `uiAccess`. Daardoor werkt het niet boven vensters die als administrator draaien (Device Manager, Register-editor, Taakbeheer). Start Visual Studio als administrator om die te testen.
- **Release** (`Release|x64`): bouwt de app, publiceert, ondertekent de exe en maakt de MSI:
  `Installer\bin\Release\WindowsHelper-<versie>.msi`

Bij de eerste Release-build maakt `tools\Sign.ps1` een zelfondertekend code signing-certificaat aan in `CurrentUser\My` (`CN=WindowsHelper Code Signing`) en exporteert de publieke sleutel naar `Installer\WindowsHelper.cer`.

## Installeren
Voer de MSI uit (vraagt om beheerdersrechten). De installer:
- installeert naar `C:\Program Files\WindowsHelper`,
- zet het certificaat in *Vertrouwde basiscertificeringsinstanties* (vereist voor `uiAccess`),
- maakt een snelkoppeling in het Startmenu.

Starten met Windows wordt door de app zelf geregeld (instelling *Start met Windows*).

## Instellingen en log
- Instellingen: `%APPDATA%\WindowsHelper\settings.json`
- Log: `%APPDATA%\WindowsHelper\log.txt`

## Beperkingen
- Een nieuw venster is heel kort wit (enkele beeldverversingen) voordat de donkere overlay verschijnt; zonder code-injectie niet te voorkomen.
- Vensters die Windows Graphics Capture weigert (bijv. sommige splash screens) blijven licht.
- Afbeeldingsherkenning is heuristisch; per programma bij te stellen via *Gevoeligheid* of uit te zetten.
- De owner-koppeling deelt de invoerwachtrij met het doelprogramma; als dat programma lang bezig is, kan WindowsHelper even mee wachten.
- Het zelfondertekende basiscertificaat vertrouwt alles wat met die sleutel wordt ondertekend; de privésleutel staat alleen op de ontwikkelmachine en is niet exporteerbaar.

Zie [CHANGELOG.md](CHANGELOG.md) voor de versiegeschiedenis.
