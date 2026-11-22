using System;
using System.Collections.Generic;
using System.Linq;
using Tweaker.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeProfileStore : IProfileStore
{
    public List<AppProfile> Profiles { get; set; } = ProfileDefaults.BuiltIns().ToList();
    public ProfileSession? Session { get; set; }
    public int ProfileSaves { get; private set; }
    public int SessionSaves { get; private set; }

    public IReadOnlyList<AppProfile> LoadProfiles() => Profiles.ToList();

    public void SaveProfiles(IReadOnlyList<AppProfile> profiles)
    {
        Profiles = profiles.ToList();
        ProfileSaves++;
    }

    public ProfileSession? LoadSession() => Session;

    public void SaveSession(ProfileSession? session)
    {
        Session = session;
        SessionSaves++;
    }
}

public enum FakeCloseEffect { Exits, GoesToTray, StaysOpen }

public sealed class FakeProcessCloser : IProcessCloser
{
    public List<(int Pid, long Created)> Closed { get; } = new();
    public string? Result { get; set; }

    public FakeProcessSource? Source { get; set; }
    public FakeCloseEffect Effect { get; set; } = FakeCloseEffect.Exits;

    public string? CloseWindow(int pid, long createdTicks)
    {
        Closed.Add((pid, createdTicks));
        if (Result is null && Source is not null && Effect != FakeCloseEffect.StaysOpen)
        {
            ProcessSnapshot next = Source.Next;
            List<ProcessSample> processes = Effect == FakeCloseEffect.Exits
                ? next.Processes.Where(p => p.Pid != pid).ToList()
                : next.Processes.Select(p => p.Pid == pid ? p with { HasWindow = false } : p).ToList();
            Source.Next = next with { Processes = processes };
        }
        return Result;
    }
}

public sealed class FakeRunningExeSource : IRunningExeSource
{
    public IReadOnlySet<string>? Next { get; set; } = Names();
    public int Reads { get; private set; }

    public IReadOnlySet<string>? Read()
    {
        Reads++;
        return Next;
    }

    public static IReadOnlySet<string> Names(params string[] names) => new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
}
