using System.Linq;
using System.Text.Json;
using Xunit;
using Tweaker.Core.Json;
using Tweaker.Core.Models;

namespace Tweaker.Tests.Models;

public class TweakJsonTests
{
    private const string Sample = """
    [
      {
        "id": "gpu-hags",
        "name": "Enable HAGS",
        "category": "Graphics",
        "description": "Hardware GPU scheduling.",
        "type": "registry",
        "risk": "low",
        "requiresRestart": true,
        "apply": [
          { "hive": "HKLM", "path": "SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers", "name": "HwSchMode", "kind": "DWord", "value": 2 }
        ],
        "revert": []
      },
      {
        "id": "disable-sysmain",
        "name": "Disable SysMain",
        "category": "System",
        "description": "Stop and disable SysMain.",
        "type": "batch",
        "risk": "medium",
        "requiresRestart": false,
        "applyScript": "sc stop SysMain",
        "revertScript": "sc start SysMain"
      }
    ]
    """;

    [Fact]
    public void DeserializeCatalog_parses_registry_and_batch_tweaks()
    {
        List<Tweak> tweaks = TweakJson.DeserializeCatalog(Sample);

        Assert.Equal(2, tweaks.Count);

        Tweak hags = tweaks[0];
        Assert.Equal("gpu-hags", hags.Id);
        Assert.Equal(TweakType.Registry, hags.Type);
        Assert.Equal(RiskLevel.Low, hags.Risk);
        Assert.True(hags.RequiresRestart);
        Assert.Single(hags.Apply);
        Assert.Equal("HwSchMode", hags.Apply[0].Name);
        Assert.Equal("DWord", hags.Apply[0].Kind);
        Assert.Equal(2, hags.Apply[0].Value.GetInt32());

        Tweak sysmain = tweaks[1];
        Assert.Equal(TweakType.Batch, sysmain.Type);
        Assert.Equal("sc stop SysMain", sysmain.ApplyScript);
    }

    [Fact]
    public void SerializeCatalog_roundtrips()
    {
        List<Tweak> original = TweakJson.DeserializeCatalog(Sample);
        string json = TweakJson.SerializeCatalog(original);
        List<Tweak> again = TweakJson.DeserializeCatalog(json);

        Assert.Equal(original.Count, again.Count);
        Assert.Equal(original[0].Id, again[0].Id);
        Assert.Equal(original[0].Apply[0].Value.GetInt32(), again[0].Apply[0].Value.GetInt32());
        Assert.Equal(TweakType.Batch, again[1].Type);
    }

    [Fact]
    public void New_fields_roundtrip_and_default_when_absent()
    {
        string withFields = """
        [
          { "id":"x","name":"X","category":"System","description":"d","type":"registry",
            "risk":"high","requiresRestart":true,
            "benefit":"more fps","downsides":"less security","warning":"NOT RECOMMENDED",
            "apply":[], "revert":[],
            "detect":[ { "hive":"HKLM","path":"P","name":"N","kind":"DWord","value":0 } ] }
        ]
        """;
        Tweak t = TweakJson.DeserializeCatalog(withFields)[0];
        Assert.Equal("more fps", t.Benefit);
        Assert.Equal("less security", t.Downsides);
        Assert.Equal("NOT RECOMMENDED", t.Warning);
        Assert.Single(t.Detect);
        Assert.Equal("N", t.Detect[0].Name);

        Tweak again = TweakJson.DeserializeCatalog(TweakJson.SerializeCatalog(new[] { t }))[0];
        Assert.Equal("more fps", again.Benefit);
        Assert.Single(again.Detect);

        string legacy = """
        [ { "id":"y","name":"Y","category":"System","description":"","type":"registry",
            "risk":"low","requiresRestart":false,"apply":[],"revert":[] } ]
        """;
        Tweak leg = TweakJson.DeserializeCatalog(legacy)[0];
        Assert.Equal("", leg.Benefit);
        Assert.Equal("", leg.Downsides);
        Assert.Null(leg.Warning);
        Assert.Empty(leg.Detect);
    }

    [Fact]
    public void RunOnce_field_roundtrips_and_defaults_false()
    {
        string withRunOnce = """
        [ { "id":"x","name":"X","category":"Maintenance","description":"","type":"batch",
            "risk":"low","requiresRestart":false,"runOnce":true,"applyScript":"echo hi" } ]
        """;
        Assert.True(TweakJson.DeserializeCatalog(withRunOnce)[0].RunOnce);

        string without = """
        [ { "id":"y","name":"Y","category":"System","description":"","type":"registry",
            "risk":"low","requiresRestart":false,"apply":[],"revert":[] } ]
        """;
        Assert.False(TweakJson.DeserializeCatalog(without)[0].RunOnce);
    }

    [Fact]
    public void Value_tweaks_parse_their_power_setting()
    {
        const string json = """
        [ { "id": "v", "name": "V", "category": "Power", "type": "value", "risk": "low",
            "power": { "subgroup": "501a4d13-42af-4429-9fd1-a8218c268e20", "setting": "ee12f906-d277-404b-b6da-e5fa1a576df5",
                       "options": [ { "value": 0, "label": "Off" }, { "value": 2, "label": "Maximum power savings" } ] } } ]
        """;

        Tweak t = Assert.Single(TweakJson.DeserializeCatalog(json));
        Assert.Equal(TweakType.Value, t.Type);
        Assert.Equal("ee12f906-d277-404b-b6da-e5fa1a576df5", t.Power!.Setting);
        Assert.Equal(new[] { 0, 2 }, t.Power.Options.Select(o => o.Value));
        Assert.Equal("Maximum power savings", t.Power.Options[1].Label);
        Assert.Equal((0, 100, 1, ""), (t.Power.Min, t.Power.Max, t.Power.Step, t.Power.Unit));

        Tweak again = Assert.Single(TweakJson.DeserializeCatalog(TweakJson.SerializeCatalog(new[] { t })));
        Assert.Equal(TweakType.Value, again.Type);
        Assert.Equal(t.Power.Subgroup, again.Power!.Subgroup);
        Assert.Null(TweakJson.DeserializeCatalog(Sample)[0].Power);
    }
}
