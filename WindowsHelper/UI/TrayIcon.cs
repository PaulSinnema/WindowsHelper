using System.Drawing.Drawing2D;
using WindowsHelper.Interop;
using WindowsHelper.Settings;

namespace WindowsHelper.UI;

internal sealed class TrayIcon : IDisposable
{
    readonly NotifyIcon _icon;
    readonly ContextMenuStrip _menu;
    readonly Icon _image;

    public TrayIcon(SettingsStore store, Action openSettings, Action exit)
    {
        var g = store.Settings;
        _menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            BackColor = Theme.Panel,
            ForeColor = Theme.Fore,
            Font = Theme.UiFont,
            ShowImageMargin = false,
            ShowCheckMargin = true,
        };

        var enabled = Add("Dark mode actief", () => { g.Enabled = !g.Enabled; store.Update(); });
        var toolbar = Add("Werkbalk in titelbalk", () => { g.ShowToolbar = !g.ShowToolbar; store.Update(); });
        var autostart = Add("Start met Windows", () =>
        {
            g.StartWithWindows = !g.StartWithWindows;
            Autostart.Apply(g.StartWithWindows);
            store.Update();
        });
        _menu.Items.Add(new ToolStripSeparator());
        Add("Instellingen…", openSettings);
        Add("Afsluiten", exit);

        _menu.Opening += (_, _) =>
        {
            enabled.Checked = g.Enabled;
            toolbar.Checked = g.ShowToolbar;
            autostart.Checked = g.StartWithWindows;
        };

        _image = CreateIcon();
        _icon = new NotifyIcon
        {
            Icon = _image,
            Text = $"WindowsHelper {AppInfo.Version}",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) openSettings(); };
    }

    ToolStripMenuItem Add(string text, Action onClick)
    {
        var item = new ToolStripMenuItem(text) { ForeColor = Theme.Fore };
        item.Click += (_, _) => onClick();
        _menu.Items.Add(item);
        return item;
    }

    // Crescent moon in accent colour
    static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var gr = Graphics.FromImage(bmp))
        {
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            gr.Clear(Color.Transparent);
            using var moon = new GraphicsPath();
            moon.AddEllipse(3, 3, 26, 26);
            using var cut = new GraphicsPath();
            cut.AddEllipse(11, -1, 24, 24);
            using var region = new Region(moon);
            region.Exclude(cut);
            using var brush = new SolidBrush(Theme.Accent);
            gr.FillRegion(brush, region);
        }
        IntPtr handle = bmp.GetHicon();
        using var temp = Icon.FromHandle(handle);
        var icon = (Icon)temp.Clone();
        Native.DestroyIcon(handle);
        return icon;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _image.Dispose();
    }
}
