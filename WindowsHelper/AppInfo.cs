namespace WindowsHelper;

internal static class AppInfo
{
    public static string Version => Application.ProductVersion;

    public static readonly string DataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsHelper");
}

internal static class Log
{
    static readonly object Sync = new();

    public static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(AppInfo.DataFolder);
                File.AppendAllText(Path.Combine(AppInfo.DataFolder, "log.txt"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // logging must never crash the app
        }
    }

    public static void Error(string where, Exception ex) => Write($"{where}: {ex}");
}
