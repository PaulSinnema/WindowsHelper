using static WindowsHelper.Interop.Native;

namespace WindowsHelper.Core;

// Out-of-context WinEvent hooks: no injection, events arrive on the UI thread
internal sealed class WindowTracker : IDisposable
{
    readonly WinEventProc _proc;
    readonly List<IntPtr> _hooks = new();

    public event Action<IntPtr>? Shown, Hidden, Destroyed, Moved, Foreground, Minimized, Restored, CloakChanged;

    public WindowTracker()
    {
        _proc = OnEvent;
        Hook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND);
        Hook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND);
        Hook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_HIDE);
        Hook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE);
        Hook(EVENT_OBJECT_CLOAKED, EVENT_OBJECT_UNCLOAKED);
    }

    void Hook(uint min, uint max)
    {
        IntPtr h = SetWinEventHook(min, max, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        if (h == IntPtr.Zero) Log.Write($"SetWinEventHook {min:X}-{max:X} failed");
        else _hooks.Add(h);
    }

    void OnEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero || idObject != OBJID_WINDOW || idChild != CHILDID_SELF) return;
        try
        {
            switch (eventType)
            {
                case EVENT_SYSTEM_FOREGROUND: Foreground?.Invoke(hwnd); break;
                case EVENT_SYSTEM_MINIMIZESTART: Minimized?.Invoke(hwnd); break;
                case EVENT_SYSTEM_MINIMIZEEND: Restored?.Invoke(hwnd); break;
                case EVENT_OBJECT_SHOW: Shown?.Invoke(hwnd); break;
                case EVENT_OBJECT_HIDE: Hidden?.Invoke(hwnd); break;
                case EVENT_OBJECT_DESTROY: Destroyed?.Invoke(hwnd); break;
                case EVENT_OBJECT_LOCATIONCHANGE: Moved?.Invoke(hwnd); break;
                case EVENT_OBJECT_CLOAKED:
                case EVENT_OBJECT_UNCLOAKED: CloakChanged?.Invoke(hwnd); break;
            }
        }
        catch (Exception ex)
        {
            Log.Error("WinEvent", ex);
        }
    }

    public void Dispose()
    {
        foreach (var h in _hooks) UnhookWinEvent(h);
        _hooks.Clear();
    }
}
