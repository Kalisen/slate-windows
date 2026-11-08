using Slate.App.Platform;
using Slate.App.Shell;
using Slate.Core.Configuration;

namespace Slate.App;

internal static class Program
{
    private const string SingleInstanceMutex = @"Local\io.opus1.slate";

    [STAThread]
    private static int Main(string[] args)
    {
        Relauncher.WaitForPreviousInstance(args);
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutex, out bool isFirstInstance);
        ApplicationConfiguration.Initialize();
        if (!isFirstInstance)
        {
            Dialogs.Info("Slate is already running", "Use the Slate icon in the notification area to relaunch or quit it.");
            return 1;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error($"Unhandled exception: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error($"Fatal exception: {e.ExceptionObject}");
        try
        {
            Application.Run(new SlateApplication());
            return 0;
        }
        catch (ConfigLoadAbortedException)
        {
            Log.Info("User selected exit.");
            return 0;
        }
    }
}
