using WindowsHelper.Core;
using WindowsHelper.Settings;
using WindowsHelper.UI;

namespace WindowsHelper;

internal sealed class TrayApplicationContext : ApplicationContext
{
    readonly SettingsStore _store = new();
    readonly DarkModeManager _manager;
    readonly TrayIcon _tray;
    readonly HotkeyWindow _hotkey = new();
    GlobalSettingsForm? _settingsForm;

    public TrayApplicationContext()
    {
        Autostart.Apply(_store.Settings.StartWithWindows);
        _manager = new DarkModeManager(_store);
        _tray = new TrayIcon(_store, OpenSettings, Exit);
        _hotkey.Pressed += _manager.ToggleActive;
        _hotkey.DumpPressed += _manager.DumpActive;
    }

    void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }
        _settingsForm = new GlobalSettingsForm(_store, key => _manager.OpenAppSettings(key, null));
        _settingsForm.Show();
    }

    void Exit()
    {
        _settingsForm?.Close();
        _hotkey.Dispose();
        _manager.Dispose();
        _tray.Dispose();
        _store.Dispose();
        ExitThread();
    }
}
