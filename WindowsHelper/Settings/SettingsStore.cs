using System.Text.Json;

namespace WindowsHelper.Settings;

internal sealed class SettingsStore : IDisposable
{
    static readonly string FilePath = Path.Combine(AppInfo.DataFolder, "settings.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 500 };

    public GlobalSettings Settings { get; private set; } = new();
    public event Action? Changed;

    public SettingsStore()
    {
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Save(); };
        Load();
    }

    void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Settings = JsonSerializer.Deserialize<GlobalSettings>(File.ReadAllText(FilePath), Json) ?? new();
        }
        catch (Exception ex)
        {
            Log.Error("Settings load", ex);
        }
        Settings.Defaults ??= new();
        Settings.Apps = new Dictionary<string, AppSettings>(Settings.Apps ?? new(), StringComparer.OrdinalIgnoreCase);
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.DataFolder);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Settings, Json));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception ex)
        {
            Log.Error("Settings save", ex);
        }
    }

    public AppSettings? Find(string app) => Settings.Apps.TryGetValue(app, out var s) ? s : null;

    public AppSettings GetOrCreate(string app)
    {
        var existing = Find(app);
        if (existing != null) return existing;
        var created = Settings.Defaults.Clone();
        created.DarkMode = Settings.DarkModeForNewApps;
        Settings.Apps[app] = created;
        return created;
    }

    public bool IsDark(string app) => Find(app)?.DarkMode ?? Settings.DarkModeForNewApps;

    public AppSettings Effective(string app) => Find(app) ?? Settings.Defaults;

    public void Remove(string app)
    {
        if (Settings.Apps.Remove(app)) Update();
    }

    // Notify listeners now, write to disk debounced
    public void Update()
    {
        Changed?.Invoke();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void Dispose()
    {
        if (_saveTimer.Enabled) Save();
        _saveTimer.Dispose();
    }
}
