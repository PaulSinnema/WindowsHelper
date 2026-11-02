using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace WindowsHelper.UI;

// Large, high-contrast slider (TrackBar has no dark mode)
internal sealed class DarkSlider : Control
{
    int _min;
    int _max = 100;
    int _value;

    public event EventHandler? ValueChanged;

    public DarkSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Width = 400;
        Height = 36;
        Cursor = Cursors.Hand;
        BackColor = Theme.Back;
    }

    [DefaultValue(0)]
    public int Minimum
    {
        get => _min;
        set { _min = value; Value = _value; Invalidate(); }
    }

    [DefaultValue(100)]
    public int Maximum
    {
        get => _max;
        set { _max = value; Value = _value; Invalidate(); }
    }

    [DefaultValue(0)]
    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Clamp(value, _min, Math.Max(_min, _max));
            if (v == _value) return;
            _value = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    int Pad => Height / 2;
    Rectangle Track => new(Pad, Height / 2 - 3, Math.Max(1, Width - 2 * Pad), 6);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var t = Track;
        float frac = _max > _min ? (float)(_value - _min) / (_max - _min) : 0f;
        int x = t.Left + (int)(frac * t.Width);
        int r = Height / 3;

        using (var b = new SolidBrush(Theme.Border)) g.FillRectangle(b, t);
        using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, t.Left, t.Top, x - t.Left, t.Height);
        using (var b = new SolidBrush(Theme.Fore)) g.FillEllipse(b, x - r, Height / 2 - r, 2 * r, 2 * r);
        if (Focused)
        {
            using var p = new Pen(Theme.Accent, 2);
            g.DrawEllipse(p, x - r - 3, Height / 2 - r - 3, 2 * r + 6, 2 * r + 6);
        }
    }

    void SetFromX(int x)
    {
        var t = Track;
        float frac = Math.Clamp((x - t.Left) / (float)t.Width, 0f, 1f);
        Value = _min + (int)Math.Round(frac * (_max - _min));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button == MouseButtons.Left) SetFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Button == MouseButtons.Left) SetFromX(e.X);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Value += Math.Sign(e.Delta);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Left:
            case Keys.Down: Value--; break;
            case Keys.Right:
            case Keys.Up: Value++; break;
            case Keys.PageDown: Value -= 10; break;
            case Keys.PageUp: Value += 10; break;
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }
}
