using WindowsHelper.Settings;

namespace WindowsHelper.UI;

// General settings, opened from the tray icon
internal sealed class GlobalSettingsForm : DarkForm
{
    readonly SettingsStore _store;
    readonly Action<string> _openApp;
    readonly ListBox _list;
    readonly List<string> _keys = new();

    public GlobalSettingsForm(SettingsStore store, Action<string> openApp)
    {
        _store = store;
        _openApp = openApp;
        var g = store.Settings;

        Text = $"WindowsHelper {AppInfo.Version} – Instellingen";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(580, 820);
        MinimumSize = new Size(480, 400);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(16),
        };

        flow.Controls.Add(Ui.MakeHeader("Algemeen"));
        flow.Controls.Add(Check("WindowsHelper actief", g.Enabled, v => g.Enabled = v));
        flow.Controls.Add(Check("Start met Windows", g.StartWithWindows, v => { g.StartWithWindows = v; Autostart.Apply(v); }));
        flow.Controls.Add(Check("Werkbalk in titelbalk tonen", g.ShowToolbar, v => g.ShowToolbar = v));
        flow.Controls.Add(Check("Dark mode automatisch aan voor nieuwe programma's", g.DarkModeForNewApps, v => g.DarkModeForNewApps = v));

        flow.Controls.Add(Ui.MakeHeader("Standaard voor nieuwe programma's"));
        var defaults = new SettingsPanel(g.Defaults, false);
        defaults.Changed += store.Update;
        flow.Controls.Add(defaults);

        flow.Controls.Add(Ui.MakeHeader("Programma's"));
        _list = new ListBox
        {
            Width = 520,
            Height = 220,
            BackColor = Theme.Panel,
            ForeColor = Theme.Fore,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
        };
        _list.DoubleClick += (_, _) => EditSelected();
        flow.Controls.Add(_list);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(3, 8, 3, 0),
        };
        var toggle = Ui.MakeButton("Aan/uit");
        toggle.Click += (_, _) => ToggleSelected();
        var edit = Ui.MakeButton("Wijzigen…");
        edit.Click += (_, _) => EditSelected();
        var remove = Ui.MakeButton("Verwijderen");
        remove.Click += (_, _) => RemoveSelected();
        buttons.Controls.Add(toggle);
        buttons.Controls.Add(edit);
        buttons.Controls.Add(remove);
        flow.Controls.Add(buttons);

        flow.Controls.Add(Ui.MakeLabel("Sneltoets Ctrl+Alt+D: dark mode aan/uit voor het actieve programma."));
        Controls.Add(flow);

        FillList();
        store.Changed += FillList;
        FormClosed += (_, _) => store.Changed -= FillList;
    }

    CheckBox Check(string text, bool value, Action<bool> set)
    {
        var cb = Ui.MakeCheck(text, value);
        cb.CheckedChanged += (_, _) => { set(cb.Checked); _store.Update(); };
        return cb;
    }

    string? SelectedKey =>
        _list.SelectedIndex >= 0 && _list.SelectedIndex < _keys.Count ? _keys[_list.SelectedIndex] : null;

    void FillList()
    {
        string? selected = SelectedKey;
        _list.BeginUpdate();
        _list.Items.Clear();
        _keys.Clear();
        foreach (var kv in _store.Settings.Apps.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            _keys.Add(kv.Key);
            _list.Items.Add($"{kv.Key}   —   dark mode {(kv.Value.DarkMode ? "aan" : "uit")}");
        }
        if (selected != null) _list.SelectedIndex = _keys.IndexOf(selected);
        _list.EndUpdate();
    }

    void ToggleSelected()
    {
        if (SelectedKey is not { } key) return;
        var s = _store.Settings.Apps[key];
        s.DarkMode = !s.DarkMode;
        _store.Update();
    }

    void EditSelected()
    {
        if (SelectedKey is { } key) _openApp(key);
    }

    void RemoveSelected()
    {
        if (SelectedKey is { } key) _store.Remove(key);
    }
}
