using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Tweaker.Services;

public enum ProfileMode { CloseList, AllowList }

public sealed record AppProfile(string Id, string Name, string Description, string Trigger, bool AutoActivate,
    bool RestoreServices, ProfileMode Mode, IReadOnlyList<string> Apps, IReadOnlyList<string> Services, bool IsBuiltIn)
{
    [JsonIgnore]
    public bool HasTrigger => Trigger.Trim().Length > 0;
}

public sealed record PlannedClose(int Pid, long CreatedTicks, string Name, bool Graceful);

public sealed record PlannedStop(string Name, string DisplayName);

public sealed record ProfilePlan(IReadOnlyList<PlannedClose> Close, IReadOnlyList<PlannedStop> Stop)
{
    public static readonly ProfilePlan Nothing = new(Array.Empty<PlannedClose>(), Array.Empty<PlannedStop>());

    public bool IsEmpty => Close.Count == 0 && Stop.Count == 0;
}

public sealed record ProfileSession(string ProfileId, string ProfileName, DateTime ActivatedUtc, bool Automatic,
    bool TriggerSeen, IReadOnlyList<string> StoppedServices, IReadOnlyList<string> ManagedServices, int ClosedApps);

public interface IProfileStore
{
    IReadOnlyList<AppProfile> LoadProfiles();

    void SaveProfiles(IReadOnlyList<AppProfile> profiles);

    ProfileSession? LoadSession();

    void SaveSession(ProfileSession? session);
}

public interface IProcessCloser
{
    string? CloseWindow(int pid, long createdTicks);
}

public interface IRunningExeSource
{
    IReadOnlySet<string>? Read();
}
