using Tweaker.Core.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeWingetService : IWingetService
{
    public bool Available { get; set; } = true;
    public List<string> Installed { get; } = new();
    public Dictionary<string, InstallResult> Results { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Available_ => Available;

    public bool IsAvailable() => Available;

    public InstallResult Install(string wingetId)
    {
        Installed.Add(wingetId);
        return Results.TryGetValue(wingetId, out InstallResult? r) ? r : new InstallResult(true, 0, "Installed.");
    }
}
