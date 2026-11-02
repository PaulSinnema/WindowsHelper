using static WindowsHelper.Interop.Native;

namespace WindowsHelper.UI;

// Global hotkeys on a message-only window
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const int ToggleId = 1;    // Ctrl+Alt+D
    const int DumpId = 2;      // Ctrl+Alt+Shift+D
    const int SettingsId = 3;  // Ctrl+Alt+S
    const uint MOD_SHIFT = 0x4;

    public event Action? Pressed;
    public event Action? DumpPressed;
    public event Action? SettingsPressed;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams { Parent = new IntPtr(-3) }); // HWND_MESSAGE
        Register(ToggleId, MOD_CONTROL | MOD_ALT, Keys.D, "Ctrl+Alt+D");
        Register(DumpId, MOD_CONTROL | MOD_ALT | MOD_SHIFT, Keys.D, "Ctrl+Alt+Shift+D");
        Register(SettingsId, MOD_CONTROL | MOD_ALT, Keys.S, "Ctrl+Alt+S");
    }

    void Register(int id, uint modifiers, Keys key, string name)
    {
        if (!RegisterHotKey(Handle, id, modifiers | MOD_NOREPEAT, (uint)key))
            Log.Write($"Hotkey {name} niet beschikbaar (al in gebruik door een ander programma)");
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            switch (m.WParam.ToInt32())
            {
                case ToggleId: Pressed?.Invoke(); break;
                case DumpId: DumpPressed?.Invoke(); break;
                case SettingsId: SettingsPressed?.Invoke(); break;
            }
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterHotKey(Handle, ToggleId);
        UnregisterHotKey(Handle, DumpId);
        UnregisterHotKey(Handle, SettingsId);
        DestroyHandle();
    }
}
