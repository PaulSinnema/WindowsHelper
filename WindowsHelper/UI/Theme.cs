using WindowsHelper.Interop;

namespace WindowsHelper.UI;

// High-contrast dark palette, large font (readability first)
internal static class Theme
{
    public static readonly Color Back = Color.FromArgb(28, 28, 28);
    public static readonly Color Panel = Color.FromArgb(44, 44, 44);
    public static readonly Color Hover = Color.FromArgb(66, 66, 66);
    public static readonly Color Border = Color.FromArgb(90, 90, 90);
    public static readonly Color Fore = Color.FromArgb(235, 235, 235);
    public static readonly Color Muted = Color.FromArgb(160, 160, 160);
    public static readonly Color Accent = Color.FromArgb(255, 190, 60);

    public static readonly Font UiFont = new("Segoe UI", 12f);
    public static readonly Font HeaderFont = new("Segoe UI Semibold", 13f);

    public static readonly string IconFontName =
        FontFamily.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

    public static void ApplyDarkFrame(IntPtr hwnd)
    {
        int on = 1;
        Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
    }
}

internal class DarkForm : Form
{
    public DarkForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Back;
        ForeColor = Theme.Fore;
        Font = Theme.UiFont;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyDarkFrame(Handle);
    }
}

internal static class Ui
{
    public static Label MakeLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Theme.Fore,
        Margin = new Padding(3, 10, 3, 0),
    };

    public static Label MakeHeader(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Theme.Accent,
        Font = Theme.HeaderFont,
        Margin = new Padding(3, 18, 3, 4),
    };

    public static CheckBox MakeCheck(string text, bool value) => new()
    {
        Text = text,
        Checked = value,
        AutoSize = true,
        ForeColor = Theme.Fore,
        FlatStyle = FlatStyle.Flat,
        FlatAppearance = { BorderColor = Theme.Fore, CheckedBackColor = Theme.Accent },
        Margin = new Padding(3, 10, 3, 0),
    };

    public static Button MakeButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        BackColor = Theme.Panel,
        ForeColor = Theme.Fore,
        FlatStyle = FlatStyle.Flat,
        FlatAppearance = { BorderColor = Theme.Border, MouseOverBackColor = Theme.Hover },
        Padding = new Padding(10, 4, 10, 4),
        Margin = new Padding(0, 0, 10, 0),
    };
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Fore : Theme.Muted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        TextRenderer.DrawText(e.Graphics, "✓", e.Item.Font, e.ImageRectangle, Theme.Accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Fore;
        base.OnRenderArrow(e);
    }
}

internal sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.Panel;
    public override Color MenuBorder => Theme.Border;
    public override Color MenuItemBorder => Theme.Hover;
    public override Color MenuItemSelected => Theme.Hover;
    public override Color MenuItemSelectedGradientBegin => Theme.Hover;
    public override Color MenuItemSelectedGradientEnd => Theme.Hover;
    public override Color MenuItemPressedGradientBegin => Theme.Hover;
    public override Color MenuItemPressedGradientEnd => Theme.Hover;
    public override Color ImageMarginGradientBegin => Theme.Panel;
    public override Color ImageMarginGradientMiddle => Theme.Panel;
    public override Color ImageMarginGradientEnd => Theme.Panel;
    public override Color SeparatorDark => Theme.Border;
    public override Color SeparatorLight => Theme.Panel;
    public override Color CheckBackground => Theme.Panel;
    public override Color CheckSelectedBackground => Theme.Hover;
    public override Color CheckPressedBackground => Theme.Hover;
}
