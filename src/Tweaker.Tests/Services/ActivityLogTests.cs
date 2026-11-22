using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class ActivityLogTests
{
    private static readonly DateTime T0 = new(2026, 9, 27, 14, 3, 11);

    [Fact]
    public void Lines_carry_the_date_time_and_source()
    {
        var log = new ActivityLog(null, () => T0);

        log.Add("Tweaks", "Applied: Disable Game DVR");
        log.For("Services")("Stopped Print Spooler");

        Assert.Equal(new[] { "2026-09-27 14:03:11  [Tweaks] Applied: Disable Game DVR", "2026-09-27 14:03:11  [Services] Stopped Print Spooler" },
            log.Lines);
    }

    [Fact]
    public void History_survives_a_restart()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "Tweaker", "activity.log");
        var first = new ActivityLog(path, () => T0);
        first.Add("Tweaks", "one");
        first.Add("Tweaks", "two");

        var second = new ActivityLog(path, () => T0);

        Assert.Equal(new[] { "2026-09-27 14:03:11  [Tweaks] one", "2026-09-27 14:03:11  [Tweaks] two" }, second.Lines);
    }

    [Fact]
    public void Only_the_newest_lines_are_kept_and_the_file_is_trimmed()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "activity.log");
        File.WriteAllLines(path, Enumerable.Range(0, ActivityLog.Keep * 4).Select(i => "old " + i));
        var log = new ActivityLog(path, () => T0);

        Assert.Equal(ActivityLog.Keep, log.Lines.Count);
        Assert.Equal("old " + (ActivityLog.Keep * 4 - 1), log.Lines.Last());

        log.Add("Tweaks", "new");
        Assert.Equal(ActivityLog.Keep, log.Lines.Count);
        Assert.Equal(ActivityLog.Keep, File.ReadAllLines(path).Length);
        Assert.EndsWith("[Tweaks] new", File.ReadAllLines(path).Last());
    }

    [Fact]
    public void A_broken_file_path_never_throws()
    {
        using var dir = new TempDir();
        string file = Path.Combine(dir.Path, "a-file");
        File.WriteAllText(file, "x");
        var log = new ActivityLog(Path.Combine(file, "activity.log"), () => T0);

        log.Add("Tweaks", "still logged");

        Assert.Single(log.Lines);
    }

    [Fact]
    public void Watching_logs_settled_messages_only()
    {
        var log = new ActivityLog(null, () => T0);
        var vm = new MessageSource();
        log.Watch(vm, "Services");

        vm.Message = "Stopping Print Spooler…";
        vm.Message = "Stopped Print Spooler.";
        vm.Message = "";
        vm.Message = "End Discord? Click Confirm to end it.";
        vm.Message = "Stop Print Spooler?";

        Assert.Equal(new[] { "2026-09-27 14:03:11  [Services] Stopped Print Spooler." }, log.Lines);
    }

    private sealed class MessageSource : INotifyPropertyChanged
    {
        private string _message = "";
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Message
        {
            get => _message;
            set { _message = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message))); }
        }
    }
}
