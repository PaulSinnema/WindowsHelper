using static WindowsHelper.Interop.Native;

namespace WindowsHelper.Interop;

internal static class ZOrder
{
    const uint Flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER;

    // Owned windows always stay above their owner, also the moment the owner is activated (no flash).
    // Detached while the owner is hung, since a cross-process owner shares its input queue.
    public static void SetOwner(IntPtr wnd, IntPtr owner)
    {
        if (wnd == IntPtr.Zero) return;
        IntPtr wanted = owner != IntPtr.Zero && !IsHungAppWindow(owner) ? owner : IntPtr.Zero;
        if (GetWindowLongPtr(wnd, GWLP_HWNDPARENT) != wanted) SetWindowLongPtr(wnd, GWLP_HWNDPARENT, wanted);
    }

    // Puts wnd directly above target, without making it topmost unless target is
    public static void PlaceAbove(IntPtr wnd, IntPtr target)
    {
        if (wnd == IntPtr.Zero || target == IntPtr.Zero) return;
        IntPtr prev = GetWindow(target, GW_HWNDPREV);
        if (prev == wnd) return;

        bool targetTop = WindowInfo.IsTopmost(target);
        bool wndTop = WindowInfo.IsTopmost(wnd);

        if (wndTop && !targetTop) SetWindowPos(wnd, HWND_NOTOPMOST, 0, 0, 0, 0, Flags);
        else if (!wndTop && targetTop) SetWindowPos(wnd, HWND_TOPMOST, 0, 0, 0, 0, Flags);

        IntPtr after;
        if (prev == IntPtr.Zero) after = targetTop ? HWND_TOPMOST : HWND_TOP;
        else if (!targetTop && WindowInfo.IsTopmost(prev)) after = HWND_TOP;
        else after = prev;

        SetWindowPos(wnd, after, 0, 0, 0, 0, Flags);
    }
}
