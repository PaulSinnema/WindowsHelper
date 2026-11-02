using static WindowsHelper.Interop.Native;

namespace WindowsHelper.UI;

// Global hotkeys on a message-only window: Ctrl+Alt+D toggle, Ctrl+Alt+Shift+D diagnostics dump
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const int ToggleId = 1;
    const int DumpId = 2;
    const uint MOD_SHIFT = 0x4;

    public event Action? Pressed;
    public event Action? DumpPressed;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams { Parent = new IntPtr(-3) }); // HWND_MESSAGE
        if (!RegisterHotKey(Handle, ToggleId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.D))
            Log.Write("Hotkey Ctrl+Alt+D niet beschikbaar");
        if (!RegisterHotKey(Handle, DumpId, MOD_CONTROL | MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, (uint)Keys.D))
            Log.Write("Hotkey Ctrl+Alt+Shift+D niet beschikbaar");
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            if (id == ToggleId) Pressed?.Invoke();
            else if (id == DumpId) DumpPressed?.Invoke();
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterHotKey(Handle, ToggleId);
        UnregisterHotKey(Handle, DumpId);
        DestroyHandle();
    }
}
