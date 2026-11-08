using System.Diagnostics;
using System.Globalization;

namespace Slate.App;

/// <summary>Writes to %LOCALAPPDATA%\Slate\slate.log (restarted when it grows too large) and the debugger.</summary>
internal static class Log
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly Lock Gate = new();

    public static string FilePath { get; } = Path.Combine(AppPaths.LocalDataDirectory, "slate.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        string line = string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}");
        Debug.WriteLine(line);
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.LocalDataDirectory);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes) file.Delete();
                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
            catch (IOException)
            {
                // Logging must never take Slate down.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

/// <summary>Where Slate keeps its files.</summary>
internal static class AppPaths
{
    public const string AppName = "Slate";
    public const string SnapshotsFileName = "snapshots";
    public const string ApprovalFileName = "approved-config";

    /// <summary>The home directory <c>~</c> expands to in configs.</summary>
    public static string HomeDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Roaming data (snapshots, approval).</summary>
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    /// <summary>Machine-local data (logs).</summary>
    public static string LocalDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
}
