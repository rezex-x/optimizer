using Tweaker.Core.Models;

namespace Tweaker.Core.Services;

public sealed class RegistryStateDetector
{
    private readonly IRegistryService _registry;

    public RegistryStateDetector(IRegistryService registry) => _registry = registry;

    public TweakState Detect(Tweak tweak)
    {
        List<RegistryOp> ops = tweak.Detect.Count > 0
            ? tweak.Detect
            : (tweak.Type == TweakType.Registry ? tweak.Apply : new List<RegistryOp>());

        if (ops.Count == 0)
            return TweakState.Unknown;

        int matches = 0;
        foreach (RegistryOp op in ops)
            if (Matches(op))
                matches++;

        if (matches == ops.Count) return TweakState.Applied;
        if (matches == 0) return TweakState.NotApplied;
        return TweakState.Partial;
    }

    private bool Matches(RegistryOp op)
    {
        (bool exists, _, object? value) = _registry.Read(op.Hive, op.Path, op.Name);
        if (!exists || value is null)
            return false;

        var kind = RegistryValueConverter.ParseKind(op.Kind);
        object target = RegistryValueConverter.ToRegistryValue(op.Value, kind);
        return RegistryEquals(value, target);
    }

    private static bool RegistryEquals(object current, object target)
    {
        if (current is int ci && target is int ti) return ci == ti;
        if (current is long cl && target is long tl) return cl == tl;
        if (current is string cs && target is string ts) return string.Equals(cs, ts, StringComparison.Ordinal);
        if (current is string[] csa && target is string[] tsa) return csa.SequenceEqual(tsa);
        if (current is byte[] cba && target is byte[] tba) return cba.SequenceEqual(tba);
        return false;
    }
}
