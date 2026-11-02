using Microsoft.Win32;

namespace WindowsHelper.Settings;

internal static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "WindowsHelper";

    public static void Apply(bool enabled)
    {
#if DEBUG
        return; // never register the debug build
#else
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (enabled && Environment.ProcessPath is { } path) key.SetValue(ValueName, $"\"{path}\"");
            else key.DeleteValue(ValueName, false);
        }
        catch (Exception ex)
        {
            Log.Error("Autostart", ex);
        }
#endif
    }
}
