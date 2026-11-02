# Changelog

All notable changes to WindowsHelper.
The version number lives in `Directory.Build.props` and is bumped on every iteration.

## [1.0.16] – 2026-11-02
### Changed
- All program text in English (settings windows, tray menu, toolbar tooltips, installer, log messages).
- README and CHANGELOG in English.

### Added
- README note about the self-signed certificate that the installer adds to the trusted root store.

## [1.0.15] – 2026-11-02
### Removed
- Diagnostics from 1.0.14 (uiAccess line at startup, log on refused z-order). Root cause found: the Debug build has no `uiAccess`; run Visual Studio as administrator to test administrator windows.

## [1.0.14] – 2026-11-02
### Added
- Log at startup: whether `uiAccess` is active, whether the process is elevated, and its path.
- Log when Windows refuses a z-order change (investigation into overlays behind administrator windows such as Device Manager).

## [1.0.13] – 2026-11-02
### Fixed
- Toolbar did not appear on new dialogs (e.g. Region): the focus event arrived while the window was still invisible. The 200 ms check now catches up with the active window.

## [1.0.12] – 2026-11-02
### Removed
- Extra shortcut `Ctrl+Alt+Shift+S` (from 1.0.11) removed again; only `Ctrl+Alt+S`.

## [1.0.11] – 2026-11-02
### Added
- Diagnostics (`Ctrl+Alt+Shift+D`) also log the foreground window and the toolbar state.

## [1.0.10] – 2026-11-02
Reset to the stable base of 0.1.10. The experiments from 1.0.1–1.0.9 were reverted
(dark placeholder on new windows, pre-created overlays, capture item on a background thread,
later changes to the owner link): they caused invisible dialogs, black rectangles and delays.

### Added
- Shortcut `Ctrl+Alt+S`: open the settings for the active program.
- Shortcut overview in the general settings window; toolbar tooltips show the shortcuts.
- README.md and CHANGELOG.md.

### Changed
- Version numbering moved to 1.0.x.
- Toolbar sits in the centre of the title bar, so programs with their own title bar icons (Loxone, Chrome) keep them free. Hidden for windows narrower than ~370 px.

## [0.1.10] – 2026-11-02
Base version.

### Features
- Dark mode for any window via screen capture (Windows.Graphics.Capture) + GPU shaders (Direct3D 11).
- "Smart invert": luminance is flipped while hue is kept, with gamma so mid greys become dark; dark areas are left alone.
- Image detection: only large image areas (≥ ~120 px) stay original; the plain background around them is still inverted.
- Settings per program (exe name), toolbar in the title bar, tray icon with general settings.
- Shortcuts `Ctrl+Alt+D` (toggle dark mode) and `Ctrl+Alt+Shift+D` (diagnostics).
- Floating panels, dropdowns, menus and tooltips are darkened too.
- Overlay and toolbar are owner-linked to the target window (no flash on focus change).
- Safety net that removes orphaned overlays; the overlay disappears when the window is hidden, minimised or closed.
- Own UI fully dark, large font, high contrast.
- MSI installer (WiX 6) with self-signed certificate and `uiAccess`.
