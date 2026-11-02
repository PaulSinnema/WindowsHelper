using System.Runtime.InteropServices;
using WindowsHelper.Core;
using WindowsHelper.Interop;
using static WindowsHelper.Interop.Native;

namespace WindowsHelper.UI;

// Small button bar in the caption of the active window: dark mode toggle + settings
internal sealed class CaptionToolbar : Form
{
    const string MoonGlyph = "";
    const string GearGlyph = "";

    readonly DarkModeManager _manager;
    readonly ToolTip _tip = new();
    IntPtr _target;
    int _hover = -1;

    public CaptionToolbar(DarkModeManager manager)
    {
        _manager = manager;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Panel;
        DoubleBuffered = true;
        Text = "WindowsHelper Toolbar";
        Size = new Size(68, 26);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= (int)(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyDarkFrame(Handle);
        int pref = DWMWCP_ROUND;
        DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = new IntPtr(MA_NOACTIVATE);
            return;
        }
        base.WndProc(ref m);
    }

    public void Attach(IntPtr target)
    {
        _target = target;
        Reposition();
        Invalidate();
    }

    public string Describe() =>
        $"target=0x{_target:X} visible={Visible} bounds=({Bounds.X},{Bounds.Y},{Bounds.Width}x{Bounds.Height}) showSetting={_manager.ShowToolbarSetting}";

    public void Detach()
    {
        _target = IntPtr.Zero;
        HideBar();
    }

    public void HideBar()
    {
        if (Visible) Hide();
        if (IsHandleCreated) ZOrder.SetOwner(Handle, IntPtr.Zero);
    }

    public void Reposition()
    {
        if (_target == IntPtr.Zero) return;
        if (!WindowInfo.IsVisibleCandidate(_target))
        {
            HideBar();
            return;
        }

        double scale = WindowInfo.Scale(_target);
        int h = (int)Math.Round(26 * scale);
        int w = (int)Math.Round(34 * scale) * 2;
        RECT frame = WindowInfo.FrameBounds(_target);
        int captionH = (int)Math.Round(32 * scale);

        // Caption height from the standard caption buttons when available
        if (DwmGetWindowAttribute(_target, DWMWA_CAPTION_BUTTON_BOUNDS, out RECT cb, Marshal.SizeOf<RECT>()) == 0 && !cb.IsEmpty)
            captionH = Math.Max(cb.Height, h);

        // Centre of the caption: custom title bars (Qt, Chrome) keep their own icons at the sides
        if (frame.Width < w + (int)Math.Round(300 * scale))
        {
            HideBar();
            return;
        }
        int x = frame.Left + (frame.Width - w) / 2;
        int y = frame.Top + Math.Max(0, (captionH - h) / 2);

        SetWindowPos(Handle, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        ZOrder.SetOwner(Handle, _target);
        if (!Visible) Show();
        ZOrder.PlaceAbove(Handle, _manager.ZAnchorFor(_target));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Panel);
        int bw = ClientSize.Width / 2;
        bool dark = _target != IntPtr.Zero && _manager.IsDark(_target);
        using var font = new Font(Theme.IconFontName, Math.Max(8f, ClientSize.Height * 0.5f), GraphicsUnit.Pixel);

        for (int i = 0; i < 2; i++)
        {
            var r = new Rectangle(i * bw, 0, bw, ClientSize.Height);
            if (i == _hover)
            {
                using var b = new SolidBrush(Theme.Hover);
                g.FillRectangle(b, r);
            }
            Color c = i == 0 ? (dark ? Theme.Accent : Theme.Muted) : Theme.Fore;
            TextRenderer.DrawText(g, i == 0 ? MoonGlyph : GearGlyph, font, r, c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        using var pen = new Pen(Theme.Border);
        g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    int HitButton(Point p) => p.X < ClientSize.Width / 2 ? 0 : 1;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hit = HitButton(e.Location);
        if (hit == _hover) return;
        _hover = hit;
        _tip.SetToolTip(this, hit == 0 ? "Toggle dark mode (Ctrl+Alt+D)" : "Settings for this program (Ctrl+Alt+S)");
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || _target == IntPtr.Zero) return;
        if (HitButton(e.Location) == 0) _manager.ToggleDark(_target);
        else _manager.OpenAppSettings(_manager.AppKey(_target), Bounds);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}
