namespace Tweaker.Core.Models;

public sealed class PowerSettingSpec
{
    public string Subgroup { get; set; } = "";
    public string Setting { get; set; } = "";
    public int Min { get; set; }
    public int Max { get; set; } = 100;
    public int Step { get; set; } = 1;
    public string Unit { get; set; } = "";
    public List<PowerOption> Options { get; set; } = new();
}

public sealed class PowerOption
{
    public int Value { get; set; }
    public string Label { get; set; } = "";
}
