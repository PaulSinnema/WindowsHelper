using System.Runtime.InteropServices;
using System.Text;
using static WindowsHelper.Interop.Native;

namespace WindowsHelper.Interop;

internal static class WindowInfo
{
    static readonly uint OwnPid = (uint)Environment.ProcessId;

    static readonly HashSet<string> ExcludedClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW",
        "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow",
        "TopLevelWindowForOverflowXamlIsland", "MultitaskingViewFrame", "ForegroundStaging", "#32768",
    };

    public static long Style(IntPtr h) => GetWindowLongPtr(h, GWL_STYLE).ToInt64();
    public static long ExStyle(IntPtr h) => GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
    public static bool IsTopmost(IntPtr h) => (ExStyle(h) & WS_EX_TOPMOST) != 0;

    public static bool IsCloaked(IntPtr h) =>
        DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    public static string ClassName(IntPtr h)
    {
        var sb = new StringBuilder(256);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    // Top-level, captioned window of another process
    public static bool IsCandidate(IntPtr h)
    {
        if (h == IntPtr.Zero || !IsWindow(h) || !IsWindowVisible(h)) return false;
        if (GetAncestor(h, GA_ROOT) != h) return false;
        long style = Style(h);
        if ((style & WS_CHILD) != 0 || (style & WS_CAPTION) != WS_CAPTION) return false;
        if ((ExStyle(h) & WS_EX_TOOLWINDOW) != 0) return false;
        GetWindowThreadProcessId(h, out uint pid);
        if (pid == OwnPid) return false;
        return !ExcludedClasses.Contains(ClassName(h));
    }

    public static bool IsVisibleCandidate(IntPtr h) => IsCandidate(h) && !IsIconic(h) && !IsCloaked(h);

    static readonly HashSet<string> OverlayExcludedClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW",
        "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow",
        "TopLevelWindowForOverflowXamlIsland", "MultitaskingViewFrame", "ForegroundStaging",
    };

    // Any visible top-level window that can be darkened: also floating panels, dropdowns, menus
    public static bool IsOverlayCandidate(IntPtr h)
    {
        if (h == IntPtr.Zero || !IsWindow(h) || !IsWindowVisible(h) || IsIconic(h)) return false;
        if (GetAncestor(h, GA_ROOT) != h) return false;
        if ((Style(h) & WS_CHILD) != 0) return false;
        if ((ExStyle(h) & WS_EX_TRANSPARENT) != 0) return false; // click-through effects/shadows
        GetWindowThreadProcessId(h, out uint pid);
        if (pid == OwnPid) return false;
        if (OverlayExcludedClasses.Contains(ClassName(h)) || IsCloaked(h)) return false;
        RECT r = FrameBounds(h);
        return r.Width >= 40 && r.Height >= 16;
    }

    public static RECT FrameBounds(IntPtr h)
    {
        if (DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT r, Marshal.SizeOf<RECT>()) == 0 && !r.IsEmpty)
            return r;
        GetWindowRect(h, out r);
        return r;
    }

    public static double Scale(IntPtr h)
    {
        uint dpi = GetDpiForWindow(h);
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    // Settings key: lower-case exe name (UWP: hosted app instead of ApplicationFrameHost)
    public static string AppKey(IntPtr h)
    {
        IntPtr source = h;
        if (ClassName(h) == "ApplicationFrameWindow")
        {
            IntPtr core = FindWindowEx(h, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);
            if (core != IntPtr.Zero) source = core;
        }
        GetWindowThreadProcessId(source, out uint pid);
        return ProcessName(pid) ?? $"pid{pid}";
    }

    static string? ProcessName(uint pid)
    {
        IntPtr hp = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hp == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            uint size = (uint)sb.Capacity;
            return QueryFullProcessImageName(hp, 0, sb, ref size)
                ? Path.GetFileName(sb.ToString()).ToLowerInvariant()
                : null;
        }
        finally
        {
            CloseHandle(hp);
        }
    }
}
