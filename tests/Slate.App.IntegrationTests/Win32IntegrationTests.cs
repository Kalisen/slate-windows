using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Slate.App.Input;
using Slate.App.Interop;
using Slate.App.Platform;
using Slate.App.Windowing;
using Slate.Core.Configuration;
using Slate.Core.Keyboard;
using Slate.Core.Windowing;
using CorePoint = Slate.Core.Geometry.Point;
using CoreRect = Slate.Core.Geometry.Rect;
using CoreSize = Slate.Core.Geometry.Size;

namespace Slate.App.IntegrationTests;

/// <summary>A real top-level window running its own message loop on an STA thread.</summary>
internal sealed class TestWindow : IDisposable
{
    private readonly Thread thread;
    private readonly ManualResetEventSlim created = new();
    private Form? form;

    public TestWindow(System.Drawing.Rectangle bounds)
    {
        Title = $"Slate integration test {Guid.NewGuid():N}";
        thread = new Thread(() =>
        {
            form = new Form { Text = Title, StartPosition = FormStartPosition.Manual, Bounds = bounds, ShowInTaskbar = true };
            form.Shown += (_, _) => created.Set();
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(created.Wait(TimeSpan.FromSeconds(10)), "The test window did not open");
    }

    public string Title { get; }

    public IWindow Find() => new Win32WindowSystem(excludedProcessId: -1).Applications()
        .SelectMany(a => a.Windows).Single(w => w.Title == Title);

    public void Dispose()
    {
        form?.Invoke(form.Close);
        thread.Join(TimeSpan.FromSeconds(10));
        created.Dispose();
    }
}

public class Win32WindowTests
{
    [Fact]
    public void Enumerates_a_window_with_its_visible_frame()
    {
        using var test = new TestWindow(new System.Drawing.Rectangle(200, 200, 640, 480));
        IWindow window = test.Find();
        Assert.Equal(Environment.ProcessId, window.Application.ProcessId);
        Assert.True(window.Application.IsNamed(Path.GetFileNameWithoutExtension(Environment.ProcessPath!)));
        CoreRect frame = window.Frame;
        Assert.InRange(frame.X, 200, 220);
        Assert.InRange(frame.Width, 600, 640);
        Assert.False(window.IsMinimizedOrHidden);
    }

    [Fact]
    public void Moves_and_resizes_the_visible_frame()
    {
        using var test = new TestWindow(new System.Drawing.Rectangle(200, 200, 640, 480));
        IWindow window = test.Find();
        Assert.True(window.Resize(new CoreSize(500, 400)));
        Assert.True(window.Move(new CorePoint(300, 250)));
        Assert.Equal(new CoreRect(300, 250, 500, 400), window.Frame);
    }

    [Fact]
    public void Minimized_windows_report_and_update_their_restored_frame()
    {
        using var test = new TestWindow(new System.Drawing.Rectangle(200, 200, 640, 480));
        var window = (Win32Window)test.Find();
        window.Application.Hide();
        Assert.True(window.IsMinimizedOrHidden);
        Assert.True(window.Application.IsHidden);
        Assert.InRange(window.Frame.X, 190, 220);

        Assert.True(window.Move(new CorePoint(400, 300)));
        window.Application.Show();
        Assert.False(window.Application.IsHidden);
        Assert.InRange(window.Frame.X, 390, 420);
    }

    [Fact]
    public void Lists_screens_primary_first()
    {
        var screens = new WinFormsScreenProvider().Screens();
        Assert.NotEmpty(screens);
        Assert.Equal(new CorePoint(0, 0), screens[0].Frame.TopLeft);
        Assert.True(screens[0].Scale >= 1);
    }
}

public class Win32InputTests
{
    [Fact]
    public void Input_structures_match_the_native_layout() =>
        Assert.Equal(IntPtr.Size == 8 ? 40 : 28, Marshal.SizeOf<INPUT>());

    [Fact]
    public void Letters_resolve_with_the_active_keyboard_layout() =>
        Assert.NotNull(new SystemKeyboardLayout().KeyForCharacter('a'));

    [Fact]
    public void The_keyboard_hook_installs_and_uninstalls()
    {
        using var hook = new KeyboardHook(new HotkeyDispatcher(new UiScheduler()));
        hook.Install();
    }
}

public sealed class TrustedConfigFileSourceTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("slate-tests").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private string Write(string name, byte[] contents)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, contents);
        return path;
    }

    [Fact]
    public void Reads_files_only_the_user_can_modify()
    {
        string path = Write(".slate", "﻿bind up:ctrl push up"u8.ToArray());
        var result = new TrustedConfigFileSource().Read(path);
        Assert.Equal(ConfigFileStatus.Loaded, result.Status);
        Assert.Equal("bind up:ctrl push up", result.Contents?.TrimStart('﻿'));
    }

    [Fact]
    public void Refuses_files_others_can_modify()
    {
        string path = Write(".slate", "bind up:ctrl push up"u8.ToArray());
        var info = new FileInfo(path);
        FileSecurity security = info.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            FileSystemRights.Write, AccessControlType.Allow));
        info.SetAccessControl(security);

        var result = new TrustedConfigFileSource().Read(path);
        Assert.Equal(ConfigFileStatus.Untrusted, result.Status);
        Assert.Contains("can be modified by", result.Error);
    }

    [Fact]
    public void Reports_missing_and_invalid_files()
    {
        Assert.Equal(ConfigFileStatus.Missing, new TrustedConfigFileSource().Read(Path.Combine(directory, "none")).Status);
        string invalid = Write("bad", [0xC3, 0x28]);
        Assert.Equal(ConfigFileStatus.Untrusted, new TrustedConfigFileSource().Read(invalid).Status);
    }
}
