using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using Slate.App.Interop;
using Slate.Core.Keyboard;
using Slate.Core.Operations;
using Slate.Core.Snapshots;

namespace Slate.App.Platform;

/// <summary>Starts programs for the <c>shell</c> operation, without a console window.</summary>
internal sealed class ShellRunner : IShell
{
    public bool Run(string command, IReadOnlyList<string> arguments, bool wait, string? workingDirectory)
    {
        var start = new ProcessStartInfo(command)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? AppPaths.HomeDirectory,
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        try
        {
            using Process? process = Process.Start(start);
            if (process is null) return false;
            if (wait) process.WaitForExit();
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Error($"Unable to run '{command}': {ex.Message}");
            return false;
        }
    }
}

/// <summary>Runs callbacks on the UI thread after a delay.</summary>
internal sealed class UiScheduler : IScheduler
{
    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        if (delay == Timeout.InfiniteTimeSpan) return new System.Windows.Forms.Timer();
        var timer = new System.Windows.Forms.Timer { Interval = Math.Max(1, (int)delay.TotalMilliseconds) };
        timer.Tick += (_, _) =>
        {
            timer.Dispose();
            action();
        };
        timer.Start();
        return timer;
    }
}

/// <summary>Stores snapshots in %APPDATA%\Slate\snapshots.</summary>
internal sealed class SnapshotFileStore : ISnapshotStore
{
    private readonly string path = Path.Combine(AppPaths.DataDirectory, AppPaths.SnapshotsFileName);

    public string? Load() => File.Exists(path) ? File.ReadAllText(path) : null;

    public void Save(string json)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error($"Unable to save snapshots to {path}: {ex.Message}");
        }
    }
}

/// <summary>Resolves characters to keys with the active keyboard layout, so bindings follow AZERTY, Dvorak, etc.</summary>
internal sealed class SystemKeyboardLayout : IKeyboardLayout
{
    private const short NoKey = -1;
    /// <summary>Shift, Ctrl and Alt bits of the high byte returned by VkKeyScanEx.</summary>
    private const int ShiftStateMask = 0x07;

    public Key? KeyForCharacter(char c)
    {
        nint layout = NativeMethods.GetKeyboardLayout(0);
        short scan = NativeMethods.VkKeyScanEx(char.ToLowerInvariant(c), layout);
        if (scan == NoKey) scan = NativeMethods.VkKeyScanEx(c, layout);
        if (scan == NoKey) return null;
        // Ignore characters that need modifiers on this layout (e.g. digits on AZERTY): bind the unshifted key instead.
        if ((scan >> 8 & ShiftStateMask) != 0 && char.IsLetterOrDigit(c) && UsKeyboardLayout.Instance.KeyForCharacter(c) is { } us)
            return us;
        return scan == NoKey ? null : new Key(scan & 0xFF);
    }
}

/// <summary>Registers Slate in HKCU\...\Run to start at sign-in.</summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled
    {
        get
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(AppPaths.AppName) is string value && value.Contains(Environment.ProcessPath!, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(AppPaths.AppName, $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue(AppPaths.AppName, throwOnMissingValue: false);
    }
}

/// <summary>Starts a new Slate that waits for this one to exit, then exits.</summary>
internal static class Relauncher
{
    public const string WaitForProcessArgument = "--wait-for-pid";

    public static void Relaunch()
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        start.ArgumentList.Add(WaitForProcessArgument);
        start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        try
        {
            Process.Start(start)?.Dispose();
            Log.Info("Relaunching Slate.");
            Application.Exit();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Error($"Unable to relaunch Slate: {ex.Message}");
        }
    }

    /// <summary>When started by <see cref="Relaunch"/>, waits for the previous instance to exit.</summary>
    public static void WaitForPreviousInstance(string[] args)
    {
        int index = Array.IndexOf(args, WaitForProcessArgument);
        if (index < 0 || index + 1 >= args.Length || !int.TryParse(args[index + 1], CultureInfo.InvariantCulture, out int pid)) return;
        try
        {
            using var previous = Process.GetProcessById(pid);
            previous.WaitForExit(TimeSpan.FromSeconds(10));
        }
        catch (ArgumentException)
        {
            // Already exited.
        }
    }
}
