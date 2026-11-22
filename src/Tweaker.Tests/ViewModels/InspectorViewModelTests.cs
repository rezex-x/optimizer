using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class InspectorViewModelTests
{
    private const string FortnitePath = @"C:\Users\A\AppData\Local\FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini";
    private const string Section = "/Script/FortniteGame.FortGameUserSettings";

    private const string FortniteIni =
        "[/Script/FortniteGame.FortGameUserSettings]\r\n" +
        "FrameRateLimit=240.000000\r\n" +
        "bUseVSync=False\r\n" +
        "FullscreenMode=1\r\n" +
        "CustomThing=abc\r\n" +
        "[ScalabilityGroups]\r\n" +
        "sg.ShadowQuality=0\r\n";

    private DateTime _clock = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
    private readonly FakeConfigFileStore _files = new();
    private readonly FakeRunningExeSource _running = new();
    private readonly Encoding _encoding = new UTF8Encoding(false);
    private IReadOnlyList<DetectedConfig> _detected = Array.Empty<DetectedConfig>();

    private InspectorViewModel Build()
    {
        _files.Files[FortnitePath] = new ConfigFile(FortniteIni, _encoding, false);
        return new InspectorViewModel(_files, _running, () => _detected, p => p.Replace(@"C:\Users\A\AppData\Local", "%LOCALAPPDATA%"), () => _clock);
    }

    private static ConfigEntryViewModel Entry(InspectorViewModel vm, string key) =>
        vm.Groups.SelectMany(g => g.Entries).Single(e => e.Key == key);

    [Fact]
    public async Task Opening_shows_sections_typed_editors_and_the_preset()
    {
        InspectorViewModel vm = Build();
        Assert.True(vm.IsClosed);

        await vm.OpenAsync(FortnitePath);

        Assert.True(vm.IsOpen);
        Assert.Equal("GameUserSettings.ini", vm.FileName);
        Assert.Equal(@"%LOCALAPPDATA%\FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini", vm.PathText);
        Assert.Equal("5 keys · 2 sections", vm.CountsText);
        Assert.Equal("FORTNITE PRESET", vm.PresetBadge);
        Assert.Equal(new[] { "All", Section, "ScalabilityGroups" }, vm.Sections.Select(s => s.Label));
        Assert.Equal(new[] { "[" + Section + "]", "[ScalabilityGroups]" }, vm.Groups.Select(g => g.Title));

        ConfigEntryViewModel fps = Entry(vm, "FrameRateLimit");
        Assert.True(fps.IsSlider);
        Assert.Equal((0d, 360d, "0", "360", "0 = unlimited"), (fps.Min, fps.Max, fps.MinText, fps.MaxText, fps.Hint));
        Assert.True(fps.HasHint);
        Assert.False(fps.IsGuessed);
        Assert.Equal(240d, fps.Number);
        Assert.True(Entry(vm, "bUseVSync").IsToggle);
        Assert.False(Entry(vm, "bUseVSync").IsOn);
        Assert.Equal("Windowed Fullscreen", Entry(vm, "FullscreenMode").SelectedOption!.Label);
        Assert.True(Entry(vm, "CustomThing").IsText);
        Assert.Equal("Low", Entry(vm, "sg.ShadowQuality").SelectedOption!.Label);
        Assert.Equal("A .bak backup is made on save", vm.StatusText);
    }

    [Fact]
    public async Task Edits_are_marked_and_save_rewrites_only_them()
    {
        InspectorViewModel vm = Build();
        await vm.OpenAsync(FortnitePath);

        Entry(vm, "FrameRateLimit").Number = 144;
        Entry(vm, "bUseVSync").IsOn = true;
        ConfigEntryViewModel mode = Entry(vm, "FullscreenMode");
        mode.SelectedOption = mode.Options.Single(o => o.Value == "2");

        Assert.Equal("144.000000", Entry(vm, "FrameRateLimit").Text);
        Assert.True(Entry(vm, "FrameRateLimit").IsDirty);
        Assert.Equal(3, vm.ChangedCount);
        Assert.Equal("Save 3 changes", vm.SaveText);
        Assert.True(vm.CanSave);

        await vm.SaveAsync();

        (string path, string text, Encoding encoding) = Assert.Single(_files.Saved);
        Assert.Equal(FortnitePath, path);
        Assert.Same(_encoding, encoding);
        Assert.Equal(FortniteIni
            .Replace("FrameRateLimit=240.000000", "FrameRateLimit=144.000000")
            .Replace("bUseVSync=False", "bUseVSync=True")
            .Replace("FullscreenMode=1", "FullscreenMode=2"), text);
        Assert.Equal("Saved · backup GameUserSettings.ini.bak created", vm.Message);
        Assert.Equal(0, vm.ChangedCount);
        Assert.Equal("Save", vm.SaveText);
        Assert.False(Entry(vm, "FrameRateLimit").IsDirty);
    }

    [Fact]
    public async Task Opening_another_file_never_drops_unsaved_changes()
    {
        InspectorViewModel vm = Build();
        _files.Files[@"C:\x\other.ini"] = new ConfigFile("a=1", _encoding, false);
        await vm.OpenAsync(FortnitePath);
        Entry(vm, "CustomThing").Text = "xyz";

        await vm.OpenAsync(@"C:\x\other.ini");

        Assert.Equal("GameUserSettings.ini", vm.FileName);
        Assert.Equal(1, vm.ChangedCount);
        Assert.Equal("Save or revert your changes to GameUserSettings.ini first.", vm.Message);
    }

    [Fact]
    public async Task Revert_discards_every_change()
    {
        InspectorViewModel vm = Build();
        await vm.OpenAsync(FortnitePath);
        Entry(vm, "CustomThing").Text = "xyz";

        vm.RevertCommand.Execute(null);

        Assert.Equal(0, vm.ChangedCount);
        Assert.Equal("abc", Entry(vm, "CustomThing").Text);
        Assert.Equal("Changes discarded.", vm.Message);
    }

    [Fact]
    public async Task A_failed_save_keeps_the_changes_and_a_later_save_matches_the_editor()
    {
        InspectorViewModel vm = Build();
        await vm.OpenAsync(FortnitePath);
        Entry(vm, "FrameRateLimit").Number = 60;
        _files.SaveResult = "the file is read-only — clear Read-only in its properties to save";

        await vm.SaveAsync();
        Assert.Equal("Couldn't save: the file is read-only — clear Read-only in its properties to save", vm.Message);
        Assert.Equal(1, vm.ChangedCount);

        _files.SaveResult = null;
        Entry(vm, "FrameRateLimit").Revert();
        Entry(vm, "CustomThing").Text = "xyz";
        await vm.SaveAsync();

        string saved = _files.Saved.Last().Text;
        Assert.Contains("FrameRateLimit=240.000000", saved);
        Assert.Contains("CustomThing=xyz", saved);
    }

    [Fact]
    public async Task Sections_filter_the_list()
    {
        InspectorViewModel vm = Build();
        await vm.OpenAsync(FortnitePath);

        vm.SelectSectionCommand.Execute(vm.Sections[2]);

        Assert.Equal(new[] { "[ScalabilityGroups]" }, vm.Groups.Select(g => g.Title));
        Assert.True(vm.Sections[2].IsActive);
        Assert.False(vm.Sections[0].IsActive);
    }

    [Fact]
    public async Task Files_that_cannot_be_opened_say_why()
    {
        InspectorViewModel vm = Build();
        _files.Files[@"C:\x\bad.json"] = new ConfigFile("{ nope", _encoding, false);
        _files.Files[@"C:\x\locked.ini"] = new ConfigFile("a=1", _encoding, true);

        await vm.OpenAsync(@"C:\x\missing.ini");
        Assert.Equal("Couldn't open missing.ini: file not found", vm.Message);
        Assert.False(vm.IsOpen);

        await vm.OpenAsync(@"C:\x\bad.json");
        Assert.Equal("Couldn't read bad.json — it isn't valid JSON.", vm.Message);
        Assert.False(vm.IsOpen);

        await vm.OpenAsync(@"C:\x\locked.ini");
        Assert.True(vm.IsOpen);
        Assert.Equal("locked.ini is read-only — clear Read-only in its properties to save.", vm.Message);
        Assert.Equal("", vm.PresetBadge);
    }

    [Fact]
    public async Task A_running_game_is_warned_about()
    {
        _running.Next = FakeRunningExeSource.Names("FortniteClient-Win64-Shipping.exe");
        InspectorViewModel vm = Build();

        await vm.OpenAsync(FortnitePath);

        Assert.True(vm.ShowGameWarning);
        Assert.Equal("Fortnite is running — it may overwrite this file when it exits. Close the game before saving.", vm.GameWarning);
    }

    [Fact]
    public async Task Closing_with_unsaved_changes_needs_a_second_separate_click()
    {
        InspectorViewModel vm = Build();
        await vm.OpenAsync(FortnitePath);
        Entry(vm, "CustomThing").Text = "xyz";

        vm.CloseCommand.Execute(null);
        Assert.True(vm.IsOpen);
        Assert.Equal("Confirm close", vm.CloseText);
        Assert.Equal("Close without saving 1 change? Click Close again.", vm.Message);

        _clock = _clock.AddMilliseconds(200);
        vm.CloseCommand.Execute(null);
        Assert.True(vm.IsOpen);

        _clock = _clock.AddSeconds(1);
        vm.CloseCommand.Execute(null);
        Assert.True(vm.IsClosed);
        Assert.Empty(vm.Groups);
        Assert.Equal("Close", vm.CloseText);
    }

    [Fact]
    public async Task Detected_configs_list_and_open()
    {
        _detected = new[]
        {
            new DetectedConfig("Fortnite", "GameUserSettings.ini", FortnitePath),
            new DetectedConfig("Other", "x.ini", @"C:\x\x.ini"),
        };
        InspectorViewModel vm = Build();

        await vm.LoadDetectedAsync();

        Assert.True(vm.HasDetected);
        Assert.Equal(new[] { "Fortnite", "Other" }, vm.Detected.Select(d => d.Game));
        Assert.True(vm.Detected[0].HasPreset);
        Assert.False(vm.Detected[1].HasPreset);
        Assert.StartsWith("%LOCALAPPDATA%", vm.Detected[0].DisplayPath);

        vm.OpenDetectedCommand.Execute(vm.Detected[0]);
        for (int i = 0; i < 100 && !vm.IsOpen; i++) await Task.Delay(20);
        Assert.True(vm.IsOpen);
    }

    [Fact]
    public async Task A_value_never_takes_a_line_break()
    {
        InspectorViewModel vm = Build();
        await vm.OpenAsync(FortnitePath);
        ConfigEntryViewModel custom = Entry(vm, "CustomThing");

        custom.Text = "x\r\n";
        Assert.Equal("x", custom.Text);
        custom.Text = "a\nb";
        Assert.Equal("ab", custom.Text);
    }

    [Fact]
    public async Task An_edit_made_while_saving_stays_unsaved()
    {
        InspectorViewModel vm = Build();
        await vm.OpenAsync(FortnitePath);
        ConfigEntryViewModel custom = Entry(vm, "CustomThing");
        custom.Text = "xyz";
        bool canRevertDuringSave = true;
        _files.DuringSave = () =>
        {
            canRevertDuringSave = vm.CanRevert;
            custom.Text = "typed-during-save";
        };

        await vm.SaveAsync();

        Assert.False(canRevertDuringSave);
        Assert.Contains("CustomThing=xyz", _files.Saved.Single().Text);
        Assert.Equal("xyz", custom.Original);
        Assert.True(custom.IsDirty);
        Assert.Equal(1, vm.ChangedCount);
        Assert.True(vm.CanRevert);
    }

    [Fact]
    public async Task Apex_is_warned_about_under_its_dx12_executable_too()
    {
        const string apexPath = @"C:\Users\A\Saved Games\Respawn\Apex\local\videoconfig.txt";
        InspectorViewModel vm = Build();
        _files.Files[apexPath] = new ConfigFile("\"VideoConfig\"\n{\n\t\"setting.fullscreen\"\t\t\"1\"\n}\n", _encoding, false);
        _running.Next = FakeRunningExeSource.Names("r5apex_dx12.exe");

        await vm.OpenAsync(apexPath);

        Assert.Equal("APEX LEGENDS PRESET", vm.PresetBadge);
        Assert.Equal("Apex Legends is running — it may overwrite this file when it exits. Close the game before saving.", vm.GameWarning);
    }

    [Fact]
    public async Task Showing_the_tab_again_rechecks_the_running_game()
    {
        InspectorViewModel vm = Build();
        await vm.RefreshGameWarningAsync();
        Assert.Equal(0, _running.Reads);

        await vm.OpenAsync(FortnitePath);
        Assert.False(vm.ShowGameWarning);

        _running.Next = FakeRunningExeSource.Names("FortniteClient-Win64-Shipping.exe");
        await vm.RefreshGameWarningAsync();
        Assert.True(vm.ShowGameWarning);

        _running.Next = FakeRunningExeSource.Names();
        await vm.RefreshGameWarningAsync();
        Assert.False(vm.ShowGameWarning);
    }

    [Fact]
    public async Task Json_comments_are_warned_about_unless_the_file_is_read_only()
    {
        InspectorViewModel vm = Build();
        _files.Files[@"C:\x\a.json"] = new ConfigFile("{\n  // frame cap\n  \"fps\": 60\n}", _encoding, false);
        _files.Files[@"C:\x\b.json"] = new ConfigFile("{ /* c */ \"fps\": 60 }", _encoding, true);
        _files.Files[@"C:\x\c.json"] = new ConfigFile("{ \"fps\": 60 }", _encoding, false);

        await vm.OpenAsync(@"C:\x\a.json");
        Assert.Equal("This file's comments will be removed on save (JSON has no comments).", vm.Message);
        vm.CloseCommand.Execute(null);

        await vm.OpenAsync(@"C:\x\b.json");
        Assert.Equal("b.json is read-only — clear Read-only in its properties to save.", vm.Message);
        vm.CloseCommand.Execute(null);

        await vm.OpenAsync(@"C:\x\c.json");
        Assert.Equal("", vm.Message);
    }

    [Fact]
    public async Task The_number_box_accepts_numbers_in_the_original_style_and_ignores_the_rest()
    {
        InspectorViewModel vm = Build();
        await vm.OpenAsync(FortnitePath);
        ConfigEntryViewModel fps = Entry(vm, "FrameRateLimit");

        fps.NumberText = "90";
        Assert.Equal("90.000000", fps.Text);
        fps.NumberText = "abc";
        Assert.Equal("90.000000", fps.Text);
        fps.NumberText = "999";
        Assert.Equal("360.000000", fps.Text);
    }
}
