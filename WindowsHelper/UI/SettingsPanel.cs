using WindowsHelper.Settings;

namespace WindowsHelper.UI;

// Edits one AppSettings instance in place
internal sealed class SettingsPanel : FlowLayoutPanel
{
    public event Action? Changed;

    public SettingsPanel(AppSettings s, bool showDarkToggle)
    {
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Theme.Back;
        ForeColor = Theme.Fore;

        if (showDarkToggle) AddCheck("Dark mode for this program", s.DarkMode, v => s.DarkMode = v);
        AddSlider("Background (brightness)", 0, 40, s.Background, v => s.Background = v);
        AddSlider("Text (brightness)", 50, 100, s.Text, v => s.Text = v);
        AddCheck("Do not invert images", s.ProtectImages, v => s.ProtectImages = v);
        AddSlider("Image detection sensitivity", 0, 100, s.ImageSensitivity, v => s.ImageSensitivity = v);
        AddCheck("Do not invert dark areas", s.KeepDarkAreas, v => s.KeepDarkAreas = v);
    }

    void AddCheck(string text, bool value, Action<bool> set)
    {
        var cb = Ui.MakeCheck(text, value);
        cb.CheckedChanged += (_, _) => { set(cb.Checked); Changed?.Invoke(); };
        Controls.Add(cb);
    }

    void AddSlider(string text, int min, int max, int value, Action<int> set)
    {
        var label = Ui.MakeLabel($"{text}: {value}%");
        var slider = new DarkSlider { Minimum = min, Maximum = max, Value = value };
        slider.ValueChanged += (_, _) =>
        {
            label.Text = $"{text}: {slider.Value}%";
            set(slider.Value);
            Changed?.Invoke();
        };
        Controls.Add(label);
        Controls.Add(slider);
    }
}
