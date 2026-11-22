using System;
using System.Threading;
using Tweaker.Core.Models;
using Tweaker.Core.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class GatedTweakEngine : ITweakEngine
{
    private volatile bool _applied;
    public ManualResetEventSlim Entered { get; } = new(false);
    public ManualResetEventSlim Release { get; } = new(false);
    public int ApplyCalls;

    public TweakState GetState(Tweak tweak) => _applied ? TweakState.Applied : TweakState.NotApplied;

    public void Apply(Tweak tweak)
    {
        Interlocked.Increment(ref ApplyCalls);
        Entered.Set();
        Release.Wait(TimeSpan.FromSeconds(10));
        _applied = true;
    }

    public void Revert(Tweak tweak) => _applied = false;
}
