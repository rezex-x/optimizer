using System.Linq;
using Tweaker.Core.Models;
using Tweaker.Core.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeTweakEngine : ITweakEngine
{
    public List<string> Applied { get; } = new();
    public List<string> Reverted { get; } = new();
    public Dictionary<string, TweakState> Overrides { get; } = new();

    public bool ThrowOnApply { get; set; }

    public TweakState GetState(Tweak tweak)
    {
        if (Overrides.TryGetValue(tweak.Id, out var forced))
            return forced;

        if (tweak.RunOnce)
            return TweakState.NotApplied;

        int applies = Applied.Count(id => id == tweak.Id);
        int reverts = Reverted.Count(id => id == tweak.Id);
        return applies > reverts ? TweakState.Applied : TweakState.NotApplied;
    }

    public void Apply(Tweak tweak)
    {
        if (ThrowOnApply) throw new InvalidOperationException("apply failed");
        Applied.Add(tweak.Id);
    }
    public void Revert(Tweak tweak) => Reverted.Add(tweak.Id);
}
