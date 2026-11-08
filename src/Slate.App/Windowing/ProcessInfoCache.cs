using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using Slate.App.Interop;

namespace Slate.App.Windowing;

/// <summary>Display name, executable and icon of a process.</summary>
internal sealed record ProcessDetails(string Name, string ExecutableName, string? ExecutablePath);

/// <summary>Caches process details by process id: reading version info on every key press would be slow.</summary>
internal static class ProcessInfoCache
{
    private const int MaxPath = 32768;
    private const int IconSize = 256;
    private static readonly ConcurrentDictionary<int, ProcessDetails> Details = new();
    private static readonly ConcurrentDictionary<string, Bitmap?> Icons = new(StringComparer.OrdinalIgnoreCase);

    public static ProcessDetails For(int processId)
    {
        if (Details.TryGetValue(processId, out var cached)) return cached;
        var details = Read(processId);
        // Process ids are reused: only cache what could be read, so a failed lookup is retried.
        if (details.ExecutablePath is not null) Details[processId] = details;
        return details;
    }

    /// <summary>Forgets processes that are no longer running.</summary>
    public static void Retain(IEnumerable<int> liveProcessIds)
    {
        var live = liveProcessIds.ToHashSet();
        foreach (int pid in Details.Keys.Where(pid => !live.Contains(pid)).ToList()) Details.TryRemove(pid, out _);
    }

    public static Bitmap? IconFor(ProcessDetails details)
    {
        if (details.ExecutablePath is null) return null;
        return Icons.GetOrAdd(details.ExecutablePath, path =>
        {
            try
            {
                using Icon? icon = Icon.ExtractIcon(path, 0, IconSize) ?? Icon.ExtractAssociatedIcon(path);
                return icon?.ToBitmap();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return null;
            }
        });
    }

    private static ProcessDetails Read(int processId)
    {
        string? path = ExecutablePath(processId);
        string exeName = path is null ? $"pid {processId}" : Path.GetFileNameWithoutExtension(path);
        string name = exeName;
        if (path is not null)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(info.FileDescription)) name = info.FileDescription.Trim();
                else if (!string.IsNullOrWhiteSpace(info.ProductName)) name = info.ProductName.Trim();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Keep the executable name.
            }
        }
        return new ProcessDetails(name, exeName, path);
    }

    private static string? ExecutablePath(int processId)
    {
        nint process = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)processId);
        if (process == 0) return null;
        try
        {
            var buffer = new char[MaxPath];
            int size = buffer.Length;
            return NativeMethods.QueryFullProcessImageName(process, 0, buffer, ref size) ? new string(buffer, 0, size) : null;
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }
}
