# WindowsHelper

Forces dark mode on any window in Windows, configurable per program.
Made for better readability (e.g. with macular degeneration): white backgrounds become dark, text becomes light, images stay original.

## Features
- Dark mode per program, on/off and adjustable (background, text brightness, images, dark areas).
- Optional toolbar in the centre of the active window's title bar: moon = on/off, gear = settings for that program.
- Tray icon: enable/disable everything, toolbar, start with Windows, general settings, list of programs.
- Keyboard shortcuts (see below) – full control without anything on screen.
- Also works over windows running as administrator (`uiAccess`).

## Keyboard shortcuts
All shortcuts are global and apply to the **active program** (the window with focus).

| Shortcut | Action |
|---|---|
| `Ctrl+Alt+D` | Toggle dark mode for the active program. The choice is remembered per program. |
| `Ctrl+Alt+S` | Open the settings window for the active program (top right of that window). Close with `Esc`. |
| `Ctrl+Alt+Shift+D` | Diagnostics: writes `dump-source.png`, `dump-result.png` and the geometry of all overlays to `%APPDATA%\WindowsHelper`. No visible feedback. |

In the settings window:
- `Tab` / `Shift+Tab` – next/previous setting
- `Space` – toggle a checkbox
- Arrow keys `←` `→` – slider by 1 %, `PageUp` / `PageDown` by 10 %; the mouse wheel works too
- `Esc` – close

If a shortcut is already used by another program, the log says `Hotkey … not available`.
The overview is also shown in the general settings window (tray icon → *Settings…*).

## Toolbar in the title bar
Optional (tray icon → *Toolbar in title bar*). Sits in the centre of the active window's title bar,
so programs that draw their own title bar (Loxone Config, Chrome, VS Code) keep their icons free.
Hidden for windows narrower than ~370 px. Prefer shortcuts only? Turn the toolbar off.

## How it works
1. **Detection** – `SetWinEventHook` (out-of-context, no DLL injection) reports windows being shown, moved, focused, minimised and closed.
2. **Capture** – Windows.Graphics.Capture records the target window (also when it is covered).
3. **Processing** – Direct3D 11 compute and pixel shaders (via Vortice):
   - classification per 8 px tile (photo / colour / plain background),
   - only large image areas are protected; the background colour around them is still inverted,
   - "smart invert": luminance is flipped while hue is kept; dark areas are left alone.
4. **Display** – a click-through overlay (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE`) lies exactly over the window.
   The overlay appears once the first dark frame is ready and is owner-linked to the target window so it follows focus changes.

## Project structure
```
WindowsHelper.sln
Directory.Build.props        Version number (bump on every iteration)
README.md / CHANGELOG.md
WindowsHelper/               WinForms tray app (.NET 10, x64)
  Core/                      WindowTracker, DarkModeManager, DarkOverlay
  Rendering/                 GpuContext, CaptureRenderer, CaptureHelper, Shaders (HLSL)
  Interop/                   P/Invoke, window info, z-order
  Settings/                  Settings (JSON), autostart
  UI/                        Theme, toolbar, tray, forms, slider, hotkeys
Installer/                   WiX 6 MSI (Package.wxs)
tools/Sign.ps1               Create certificate + sign the exe
```

## Requirements
- Windows 10 2004+ / Windows 11, x64
- .NET 10 Desktop Runtime
- Development: Visual Studio 2026 (18.x) with the .NET 10 SDK

## Building
- **Debug** (`Debug|x64`): runs straight from Visual Studio, without `uiAccess`. Therefore it does not work over windows running as administrator (Device Manager, Registry Editor, Task Manager). Run Visual Studio as administrator to test those.
- **Release** (`Release|x64`): builds the app, publishes it, signs the exe and creates the MSI:
  `Installer\bin\Release\WindowsHelper-<version>.msi`

On the first Release build, `tools\Sign.ps1` creates a self-signed code signing certificate in `CurrentUser\My` (`CN=WindowsHelper Code Signing`, non-exportable key) and exports the public part to `Installer\WindowsHelper.cer`.

## Installing
Run the MSI (asks for administrator rights). The installer:
- installs to `C:\Program Files\WindowsHelper`,
- adds the WindowsHelper certificate to *Trusted Root Certification Authorities* (see the note below),
- creates a Start menu shortcut.

Start with Windows is handled by the app itself (setting *Start with Windows*).

> [!NOTE]
> **About the certificate.** To work over administrator windows, Windows requires WindowsHelper to be signed with a trusted
> certificate. WindowsHelper uses its own self-signed certificate, which the installer adds to your trusted certificates.
> Your PC then also trusts anything else signed with that certificate, so only install WindowsHelper if you trust the author.
> Windows may show *"Unknown publisher"* during installation. Uninstalling WindowsHelper removes the certificate again.

## Settings and log
- Settings: `%APPDATA%\WindowsHelper\settings.json`
- Log: `%APPDATA%\WindowsHelper\log.txt`

## Limitations
- A new window is briefly white (a few frames) before the dark overlay appears; this cannot be avoided without code injection.
- Windows that Windows Graphics Capture refuses (e.g. some splash screens) stay light.
- Image detection is heuristic; adjust per program via *Image detection sensitivity* or turn it off.
- The owner link shares the input queue with the target program; if that program is busy for a long time, WindowsHelper may wait along with it.

See [CHANGELOG.md](CHANGELOG.md) for the version history.
