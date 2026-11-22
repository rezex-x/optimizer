using Tweaker.Core.Catalog;
using Tweaker.Core.Models;

namespace Tweaker.Core.Services;

public sealed class WindowsUpdateService : IWindowsUpdateService
{
    private readonly IRegistryService _registry;
    private readonly IBackupService _backup;
    private readonly IProcessRunner _runner;

    public WindowsUpdateService(IRegistryService registry, IBackupService backup, IProcessRunner runner)
    {
        _registry = registry;
        _backup = backup;
        _runner = runner;
    }

    public UpdateApplyResult Apply(UpdatePreset preset)
    {
        RegistryOp[] ops = UpdatePresets.Ops(preset);

        BackupRecord record = _backup.Capture("windows-update", ops);
        _backup.Persist(record);

        foreach (RegistryOp op in ops)
        {
            var kind = RegistryValueConverter.ParseKind(op.Kind);
            object value = RegistryValueConverter.ToRegistryValue(op.Value, kind);
            _registry.Set(op.Hive, op.Path, op.Name, kind, value);
        }

        ProcessResult svc = _runner.Run("cmd.exe", $"/c \"{UpdatePresets.ServiceCommand(preset)}\"");

        bool ok = svc.ExitCode == 0;
        string msg = ok ? $"{preset} applied." : $"Applied registry policy; service step exit {svc.ExitCode}.";
        return new UpdateApplyResult(ok, msg);
    }

    public UpdatePreset DetectActive()
    {
        if (ReadDword("HKLM", UpdatePresets.AuPath, "NoAutoUpdate") == 1)
            return UpdatePreset.DisableAll;
        if (ReadDword("HKLM", UpdatePresets.PolicyPath, "DeferFeatureUpdates") == 1)
            return UpdatePreset.SecurityOnly;
        return UpdatePreset.Default;
    }

    private int ReadDword(string hive, string path, string name)
    {
        (bool exists, _, object? value) = _registry.Read(hive, path, name);
        return exists && value is not null ? Convert.ToInt32(value) : 0;
    }
}
