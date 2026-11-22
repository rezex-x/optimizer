using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class ProcessRulesTests
{
    private const string WinDir = @"C:\Windows";

    private static ProcessSample P(int pid, string name, string? path = null, bool window = false, bool critical = false) =>
        new(pid, 1, name, path, null, 0, null, "User", window, critical);

    [Fact]
    public void Core_windows_security_and_anti_cheat_processes_are_protected()
    {
        Assert.True(ProcessRules.IsProtected(P(4, "System"), selfPid: 9999));
        Assert.True(ProcessRules.IsProtected(P(700, "csrss.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(1024, "explorer.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(3840, "MsMpEng.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(5000, "EasyAntiCheat_EOS.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(5001, "vgc.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(6100, "TrustedInstaller.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(6200, "PnkBstrA.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(6300, "WUDFHost.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(6400, "SGuard64.exe"), 9999));
    }

    [Fact]
    public void Per_game_anti_cheat_wrappers_are_protected_by_name_pattern()
    {
        Assert.True(ProcessRules.IsProtected(P(7000, "TslGame_BE.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(7001, "FortniteClient-Win64-Shipping_EAC_EOS.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(7002, "Game_EAC.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(7003, "start_protected_game.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(7004, "START_PROTECTED_GAME.EXE"), 9999));
        Assert.True(ProcessRules.IsProtected(P(7005, "BEClient_x64.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(7006, "EasyAntiCheat_Setup.exe"), 9999));
        Assert.True(ProcessRules.IsProtected(P(7007, "tslgame_be.EXE"), 9999));
        Assert.False(ProcessRules.IsProtected(P(7008, "notepad.exe"), 9999));
    }

    [Fact]
    public void Critical_processes_and_this_app_are_protected_but_ordinary_apps_are_not()
    {
        Assert.True(ProcessRules.IsProtected(P(812, "anything.exe", critical: true), 9999));
        Assert.True(ProcessRules.IsProtected(P(9999, "tweaker.exe"), 9999));
        Assert.False(ProcessRules.IsProtected(P(4820, "chrome.exe"), 9999));
    }

    [Fact]
    public void Kind_puts_the_windows_folder_and_unknown_images_under_windows()
    {
        Assert.Equal(ProcessKind.Windows, ProcessRules.Kind(P(4, "System"), WinDir));
        Assert.Equal(ProcessKind.Windows, ProcessRules.Kind(P(812, "csrss.exe", path: null), WinDir));
        Assert.Equal(ProcessKind.Windows, ProcessRules.Kind(P(1024, "explorer.exe", @"C:\Windows\explorer.exe", window: true), WinDir));
        Assert.Equal(ProcessKind.Windows, ProcessRules.Kind(P(2288, "spoolsv.exe", @"c:\windows\System32\spoolsv.exe"), WinDir + @"\"));
    }

    [Fact]
    public void Kind_splits_other_processes_by_visible_window()
    {
        Assert.Equal(ProcessKind.App,
            ProcessRules.Kind(P(4820, "chrome.exe", @"C:\Program Files\Google\Chrome\Application\chrome.exe", window: true), WinDir));
        Assert.Equal(ProcessKind.Background,
            ProcessRules.Kind(P(2044, "OneDrive.exe", @"C:\Users\player\AppData\Local\Microsoft\OneDrive\OneDrive.exe"), WinDir));
        Assert.Equal(ProcessKind.Background,
            ProcessRules.Kind(P(77, "helper.exe", @"C:\WindowsApps\helper.exe"), WinDir));
    }
}
