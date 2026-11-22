using Tweaker.Core.Models;

namespace Tweaker.Core.Services;

public sealed class TweakEngine : ITweakEngine
{
    private readonly IRegistryService _registry;
    private readonly IBackupService _backup;
    private readonly BatchService _batch;
    private readonly RegistryStateDetector _detector;
    private readonly IAppliedTweakStore _applied;

    public TweakEngine(IRegistryService registry, IBackupService backup, BatchService batch, RegistryStateDetector detector,
        IAppliedTweakStore? applied = null)
    {
        _registry = registry;
        _backup = backup;
        _batch = batch;
        _detector = detector;
        _applied = applied ?? new InMemoryAppliedTweakStore();
    }

    public TweakState GetState(Tweak tweak)
    {
        TweakState detected = _detector.Detect(tweak);
        if (detected == TweakState.Unknown && tweak.Type == TweakType.Batch && !tweak.RunOnce)
            return _applied.IsApplied(tweak.Id) ? TweakState.Applied : TweakState.NotApplied;
        return detected;
    }

    public void Apply(Tweak tweak)
    {
        if (tweak.Type == TweakType.Value)
            throw new NotSupportedException("Value tweaks are edited on their row, not applied by the engine.");

        if (tweak.Type == TweakType.Batch)
        {
            _batch.ApplyScript(tweak);
            if (!tweak.RunOnce) _applied.SetApplied(tweak.Id, true);
            return;
        }

        BackupRecord record = _backup.Capture(tweak.Id, tweak.Apply);
        _backup.Persist(record);

        foreach (RegistryOp op in tweak.Apply)
        {
            var kind = RegistryValueConverter.ParseKind(op.Kind);
            object value = RegistryValueConverter.ToRegistryValue(op.Value, kind);
            _registry.Set(op.Hive, op.Path, op.Name, kind, value);
        }
    }

    public void Revert(Tweak tweak)
    {
        if (tweak.Type == TweakType.Value)
            throw new NotSupportedException("Value tweaks are edited on their row, not applied by the engine.");

        if (tweak.Type == TweakType.Batch)
        {
            _batch.RevertScript(tweak);
            _applied.SetApplied(tweak.Id, false);
            return;
        }

        BackupRecord? latest = _backup.LoadAll()
            .Where(r => r.TweakId == tweak.Id)
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefault();

        if (latest is not null)
        {
            _backup.Restore(latest);
            return;
        }

        foreach (RegistryOp op in tweak.Revert)
        {
            var kind = RegistryValueConverter.ParseKind(op.Kind);
            object value = RegistryValueConverter.ToRegistryValue(op.Value, kind);
            _registry.Set(op.Hive, op.Path, op.Name, kind, value);
        }
    }
}
