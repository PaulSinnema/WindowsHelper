namespace WindowsHelper.Settings;

public sealed class AppSettings
{
    public bool DarkMode { get; set; } = true;
    public int Background { get; set; } = 8;        // % brightness of new background
    public int Text { get; set; } = 88;             // % brightness of new text
    public bool ProtectImages { get; set; } = true;
    public int ImageSensitivity { get; set; } = 50; // % image detection sensitivity
    public bool KeepDarkAreas { get; set; } = true;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    public void CopyLookFrom(AppSettings o)
    {
        Background = o.Background;
        Text = o.Text;
        ProtectImages = o.ProtectImages;
        ImageSensitivity = o.ImageSensitivity;
        KeepDarkAreas = o.KeepDarkAreas;
    }
}

public sealed class GlobalSettings
{
    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public bool ShowToolbar { get; set; } = true;
    public bool DarkModeForNewApps { get; set; }
    public AppSettings Defaults { get; set; } = new();
    public Dictionary<string, AppSettings> Apps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
