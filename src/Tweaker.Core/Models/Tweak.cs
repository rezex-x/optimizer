namespace Tweaker.Core.Models;

public sealed class Tweak
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public TweakType Type { get; set; }
    public RiskLevel Risk { get; set; }
    public bool RequiresRestart { get; set; }
    public List<RegistryOp> Apply { get; set; } = new();
    public List<RegistryOp> Revert { get; set; } = new();
    public string? ApplyScript { get; set; }
    public string? RevertScript { get; set; }
    public string Benefit { get; set; } = "";
    public string Downsides { get; set; } = "";
    public string? Warning { get; set; }
    public List<RegistryOp> Detect { get; set; } = new();
    public bool RunOnce { get; set; }

    public int? FreesRamMb { get; set; }

    public PowerSettingSpec? Power { get; set; }
}
