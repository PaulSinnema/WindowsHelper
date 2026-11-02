using WindowsHelper.Settings;

namespace WindowsHelper.UI;

// Per-program settings, opened from the caption toolbar
internal sealed class AppSettingsForm : DarkForm
{
    readonly SettingsStore _store;
    readonly AppSettings _settings;
    readonly FlowLayoutPanel _flow;
    SettingsPanel _panel;

    public AppSettingsForm(SettingsStore store, string appKey)
    {
        _store = store;
        _settings = store.GetOrCreate(appKey);

        Text = $"WindowsHelper – {appKey}";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        _flow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        _panel = CreatePanel();

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 16, 0, 0),
        };
        var reset = Ui.MakeButton("Standaardwaarden");
        reset.Click += (_, _) => ResetToDefaults();
        var close = Ui.MakeButton("Sluiten");
        close.Click += (_, _) => Close();
        buttons.Controls.Add(reset);
        buttons.Controls.Add(close);

        _flow.Controls.Add(_panel);
        _flow.Controls.Add(buttons);
        Controls.Add(_flow);
        CancelButton = close;
    }

    SettingsPanel CreatePanel()
    {
        var panel = new SettingsPanel(_settings, true);
        panel.Changed += _store.Update;
        return panel;
    }

    void ResetToDefaults()
    {
        _settings.CopyLookFrom(_store.Settings.Defaults);
        _store.Update();

        int index = _flow.Controls.GetChildIndex(_panel);
        _flow.Controls.Remove(_panel);
        _panel.Dispose();
        _panel = CreatePanel();
        _flow.Controls.Add(_panel);
        _flow.Controls.SetChildIndex(_panel, index);
    }

    // Below the toolbar, or centred when opened from the tray
    public void ShowNear(Rectangle? anchor)
    {
        Show();
        var area = anchor is { } a ? Screen.FromRectangle(a).WorkingArea : Screen.PrimaryScreen!.WorkingArea;
        int x, y;
        if (anchor is { } r)
        {
            x = r.Right - Width;
            y = r.Bottom + 4;
        }
        else
        {
            x = area.Left + (area.Width - Width) / 2;
            y = area.Top + (area.Height - Height) / 2;
        }
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - Width));
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - Height));
        Location = new Point(x, y);
        Activate();
    }
}
