using Tweaker.Core.Models;

namespace Tweaker.Core.Nvidia;

public sealed class NvidiaSetting
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public uint SettingId { get; init; }
    public uint RecommendedValue { get; init; }
    public uint DefaultValue { get; init; }
    public string Benefit { get; init; } = "";
    public string Downsides { get; init; } = "";
    public RiskLevel Risk { get; init; }
    public string? Warning { get; init; }
    public string Category { get; init; } = "";
}
