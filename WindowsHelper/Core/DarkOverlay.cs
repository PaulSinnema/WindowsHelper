using WindowsHelper.Interop;
using WindowsHelper.Rendering;
using WindowsHelper.Settings;
using static WindowsHelper.Interop.Native;

namespace WindowsHelper.Core;

// Click-through window placed directly above the target, showing its inverted capture
internal sealed class DarkOverlay : IDisposable
{
    readonly OverlayWindow _window = new();
    readonly CaptureRenderer _renderer;
    RECT _bounds;
    bool? _rounded;
    bool _disposed;

    public IntPtr Target { get; }
    public IntPtr Handle => _window.Handle;
    public bool IsShown { get; private set; }

    public DarkOverlay(IntPtr target, GpuContext gpu, AppSettings settings, Action targetClosed)
    {
        Target = target;
        try
        {
            _renderer = new CaptureRenderer(gpu, target, _window.Handle, settings, Show, targetClosed);
        }
        catch
        {
            _window.DestroyHandle();
            throw;
        }
        UpdateBounds(true);
    }

    // Only show after the first frame is rendered: no white flash
    void Show()
    {
        if (_disposed || IsShown) return;
        IsShown = true;
        UpdateBounds(true);
        ShowWindow(Handle, SW_SHOWNOACTIVATE);
        EnsureZOrder();
    }

    public void UpdateBounds(bool force = false)
    {
        if (_disposed) return;
        bool zoomed = IsZoomed(Target);
        if (_rounded != !zoomed)
        {
            int pref = zoomed ? DWMWCP_DONOTROUND : DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            _rounded = !zoomed;
        }

        RECT r = WindowInfo.FrameBounds(Target);
        if (!force && r.Same(_bounds)) return;
        _bounds = r;
        SetWindowPos(Handle, IntPtr.Zero, r.Left, r.Top, r.Width, r.Height, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    public void EnsureZOrder()
    {
        if (!IsShown || _disposed) return;
        ZOrder.SetOwner(Handle, Target);
        ZOrder.PlaceAbove(Handle, Target);
    }

    public void UpdateSettings(AppSettings settings) => _renderer.UpdateSettings(settings);

    public void Dump(string folder) => _renderer.Dump(folder);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _renderer.Dispose();
        if (_window.Handle != IntPtr.Zero)
        {
            ZOrder.SetOwner(_window.Handle, IntPtr.Zero);
            _window.DestroyHandle();
        }
    }
}

internal sealed class OverlayWindow : NativeWindow
{
    public OverlayWindow()
    {
        CreateHandle(new CreateParams
        {
            Caption = "WindowsHelper Overlay",
            Style = unchecked((int)WS_POPUP),
            ExStyle = (int)(WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_NOREDIRECTIONBITMAP),
            Width = 1,
            Height = 1,
        });
        // 254 instead of 255: apps like Chrome then don't treat themselves as occluded and keep painting
        SetLayeredWindowAttributes(Handle, 0, 254, LWA_ALPHA);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            m.Result = new IntPtr(HTTRANSPARENT);
            return;
        }
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = new IntPtr(MA_NOACTIVATE);
            return;
        }
        base.WndProc(ref m);
    }
}
