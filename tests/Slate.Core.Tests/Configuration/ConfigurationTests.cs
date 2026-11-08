using Slate.Core.Configuration;
using Slate.Core.Keyboard;
using Slate.Core.Operations;

namespace Slate.Core.Tests.Configuration;

internal sealed class FakeFiles : IConfigFileSource
{
    public Dictionary<string, ConfigFileResult> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string path, string contents) => Files[Path.GetFullPath(path)] = ConfigFileResult.Loaded(contents);

    public ConfigFileResult Read(string path) => Files.GetValueOrDefault(path, ConfigFileResult.Missing);
}

internal sealed class RecordingErrors(bool quit = false) : IConfigErrorReporter
{
    public List<ConfigException> Errors { get; } = [];

    public bool ReportAndAskQuit(ConfigException error)
    {
        Errors.Add(error);
        return quit;
    }
}

public class ConfigParserTests
{
    private static readonly string UserConfig = Path.Combine(TestEnvironment.Home, ".slate");

    private static (SlateConfiguration Config, RecordingErrors Errors) Load(string text, Action<FakeFiles>? files = null, bool quit = false)
    {
        var fakeFiles = new FakeFiles();
        fakeFiles.Add(UserConfig, text);
        files?.Invoke(fakeFiles);
        var errors = new RecordingErrors(quit);
        var parser = new ConfigParser(fakeFiles, UsKeyboardLayout.Instance, TestEnvironment.Home, errors);
        var config = new SlateConfiguration();
        Assert.True(parser.LoadFile(config, "~/.slate", out _));
        return (config, errors);
    }

    [Fact]
    public void Reads_every_directive()
    {
        var (config, errors) = Load("""
            # comment
            config defaultToCurrentScreen true   # trailing comment
            config snapshotTitleMatch:'Code' sequential
            alias left move screenOriginX;screenOriginY screenSizeX/2;screenSizeY
            layout work 'Terminal' ${left}
            default work count:2
            bind left:ctrl;cmd ${left}
            bind 1:f4 layout work
            bind right:alt resize +10% +0
            """);
        Assert.Empty(errors.Errors);
        Assert.True(config.Settings.GetBool(ConfigKeys.DefaultToCurrentScreen));
        Assert.Equal("sequential", config.Settings.GetForApp(ConfigKeys.SnapshotTitleMatch, "Code"));
        Assert.True(config.Layouts.ContainsKey("work"));
        Assert.Single(config.Defaults);
        Assert.Equal(2, config.Bindings.Global.Count);
        Assert.Single(config.Bindings.Modal);
        Assert.False(config.Bindings.Global[0].RepeatOnHold);
        Assert.True(config.Bindings.Global[1].RepeatOnHold);
        Assert.IsType<MoveOperation>(config.Bindings.Global[0].Operation);
        Assert.Equal(new[] { UserConfig }, config.Review.Paths);
    }

    [Theory]
    [InlineData("config nope 1", "Unrecognized Config 'nope'")]
    [InlineData("bind left:ctrl ${missing}", "Unrecognized Alias")]
    [InlineData("bind left:ctrl teleport", "Unrecognized Operation")]
    [InlineData("bind nokey:ctrl push up", "Unrecognized Key")]
    [InlineData("default work size:3", "Error loading default layout")]
    [InlineData("source missing.slate", "Error Sourcing File 'missing.slate'")]
    public void Reports_errors_and_skips_the_line(string line, string title)
    {
        var (config, errors) = Load(line + "\nbind up:ctrl push up");
        Assert.Equal(title, Assert.Single(errors.Errors).Title);
        Assert.Single(config.Bindings.Global);
    }

    [Fact]
    public void Quitting_on_an_error_aborts_loading() =>
        Assert.Throws<ConfigLoadAbortedException>(() => Load("config nope 1", quit: true));

    [Fact]
    public void Sources_files_relative_to_home_and_tolerates_missing_optional_ones()
    {
        var (config, errors) = Load("source extra.slate\nsource ~/optional.slate if_exists\nbind up:ctrl layout extra",
            files => files.Add(Path.Combine(TestEnvironment.Home, "extra.slate"), "layout extra 'App' push up"));
        Assert.Empty(errors.Errors);
        Assert.Single(config.Bindings.Global);
        Assert.Equal(2, config.Review.Paths.Count);
    }

    [Fact]
    public void Untrusted_sourced_files_are_reported()
    {
        var (_, errors) = Load("source shared.slate", files =>
            files.Files[Path.Combine(TestEnvironment.Home, "shared.slate")] = ConfigFileResult.Untrusted("shared.slate can be modified by Everyone"));
        Assert.Contains("Everyone", Assert.Single(errors.Errors).Message);
    }

    [Fact]
    public void Undo_bindings_enable_undo_snapshots()
    {
        Assert.False(Load("bind z:ctrl push up").Config.UsesUndo);
        Assert.True(Load("bind z:ctrl undo").Config.UsesUndo);
    }

    [Fact]
    public void Switch_bindings_know_their_trigger()
    {
        var binding = Load("bind tab:alt switch back:q").Config.Bindings.Global[0];
        var op = Assert.IsType<SwitchOperation>(binding.Operation);
        Assert.Equal(binding.Keystroke.Keystroke, op.Trigger);
        Assert.Equal(new Key(VirtualKeys.LetterA + 16), op.Keys.Back);
    }

    [Fact]
    public void The_default_config_loads_without_errors()
    {
        var errors = new RecordingErrors();
        var parser = new ConfigParser(new FakeFiles(), UsKeyboardLayout.Instance, TestEnvironment.Home, errors);
        var config = new SlateConfiguration();
        parser.LoadText(config, ConfigLoader.DefaultConfigText());
        Assert.Empty(errors.Errors);
        Assert.True(config.Bindings.Global.Count > 20);
    }
}

public class ConfigApprovalTests
{
    private sealed class MemoryStore : IApprovalStore
    {
        public string? Digest { get; set; }
        public string? ApprovedDigest() => Digest;
        public void StoreApprovedDigest(string digest) => Digest = digest;
    }

    private sealed class Approver(bool answer) : IConfigApprover
    {
        public int Asked { get; private set; }

        public bool Approve(ConfigReview review)
        {
            Asked++;
            return answer;
        }
    }

    private static ConfigReview Review(string contents)
    {
        var review = new ConfigReview();
        review.AddFile(@"C:\Users\tester\.slate", contents);
        review.AddLine("bind 1:ctrl shell 'C:\\x.exe'");
        review.AddLine("bind 2:ctrl push up");
        return review;
    }

    [Fact]
    public void Digest_depends_on_paths_and_contents()
    {
        Assert.Equal(Review("a").Digest(), Review("a").Digest());
        Assert.NotEqual(Review("a").Digest(), Review("b").Digest());
        var shifted = new ConfigReview();
        shifted.AddFile("ab", "c");
        var other = new ConfigReview();
        other.AddFile("a", "bc");
        Assert.NotEqual(shifted.Digest(), other.Digest());
    }

    [Fact]
    public void Lists_shell_lines_for_review() =>
        Assert.Equal(new[] { "bind 1:ctrl shell 'C:\\x.exe'" }, Review("a").ShellLines);

    [Fact]
    public void Approval_is_remembered_until_the_config_changes()
    {
        var store = new MemoryStore();
        var approver = new Approver(true);
        var approval = new ConfigApproval(store, approver);
        Assert.True(approval.IsApproved(Review("a")));
        Assert.True(approval.IsApproved(Review("a")));
        Assert.Equal(1, approver.Asked);
        Assert.True(approval.IsApproved(Review("b")));
        Assert.Equal(2, approver.Asked);
    }

    [Fact]
    public void Rejected_configs_are_not_remembered()
    {
        var store = new MemoryStore();
        Assert.False(new ConfigApproval(store, new Approver(false)).IsApproved(Review("a")));
        Assert.Null(store.Digest);
    }

    [Fact]
    public void The_loader_falls_back_to_the_default_config_or_nothing()
    {
        var errors = new RecordingErrors();
        var parser = new ConfigParser(new FakeFiles(), UsKeyboardLayout.Instance, TestEnvironment.Home, errors);
        var missing = new ConfigLoader(parser, new ConfigApproval(new MemoryStore(), new Approver(false)), errors).Load();
        Assert.Equal(ConfigSource.Default, missing.Source);
        Assert.Equal("Could not load ~/.slate", Assert.Single(errors.Errors).Title);

        var files = new FakeFiles();
        files.Add(Path.Combine(TestEnvironment.Home, ".slate"), "bind up:ctrl push up");
        var userParser = new ConfigParser(files, UsKeyboardLayout.Instance, TestEnvironment.Home, errors);
        var rejected = new ConfigLoader(userParser, new ConfigApproval(new MemoryStore(), new Approver(false)), errors).Load();
        Assert.Equal(ConfigSource.Rejected, rejected.Source);
        Assert.Empty(rejected.Bindings.Global);

        var approved = new ConfigLoader(userParser, new ConfigApproval(new MemoryStore(), new Approver(true)), errors).Load();
        Assert.Equal(ConfigSource.User, approved.Source);
        Assert.Single(approved.Bindings.Global);
    }

    [Fact]
    public void Quitting_when_the_user_config_is_missing_aborts()
    {
        var errors = new RecordingErrors(quit: true);
        var parser = new ConfigParser(new FakeFiles(), UsKeyboardLayout.Instance, TestEnvironment.Home, errors);
        Assert.Throws<ConfigLoadAbortedException>(() =>
            new ConfigLoader(parser, new ConfigApproval(new MemoryStore(), new Approver(true)), errors).Load());
    }
}

public class SlateSettingsTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("YES", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("nope", false)]
    public void Parses_booleans_like_macOS(string value, bool expected) => Assert.Equal(expected, SlateSettings.ParseBool(value));

    [Fact]
    public void Typed_getters_fall_back_to_defaults()
    {
        var settings = new SlateSettings();
        settings.Set(ConfigKeys.WindowHintsOrder, "rightToLeft");
        settings.Set(ConfigKeys.SwitchOrientation, "diagonal");
        settings.Set(ConfigKeys.GridBackgroundColor, "1;2;3");
        Assert.Equal(WindowHintsOrder.RightToLeft, settings.GetEnum<WindowHintsOrder>(ConfigKeys.WindowHintsOrder));
        Assert.Equal(SwitchOrientation.Horizontal, settings.GetEnum<SwitchOrientation>(ConfigKeys.SwitchOrientation));
        Assert.Equal(new RgbaColor(75, 77, 81, 1.0), settings.GetColor(ConfigKeys.GridBackgroundColor));
        Assert.Equal(0.4, settings.GetDouble(ConfigKeys.SecondsBeforeRepeat));
    }

    [Fact]
    public void Unknown_keys_are_rejected() =>
        Assert.Throws<ConfigException>(() => new SlateSettings().Set("unknown", "1"));
}
