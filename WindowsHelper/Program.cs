namespace WindowsHelper;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, @"Local\WindowsHelper.SingleInstance", out bool created);
        if (!created) return;

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error("UI", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write($"Unhandled: {e.ExceptionObject}");

        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.Dark);

        Log.Write($"Start {AppInfo.Version}");
        Application.Run(new TrayApplicationContext());
    }
}
