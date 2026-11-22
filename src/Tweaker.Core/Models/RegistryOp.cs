using System.Text.Json;

namespace Tweaker.Core.Models;

public sealed class RegistryOp
{
    public string Hive { get; set; } = "HKCU";
    public string Path { get; set; } = "";
    public string? Name { get; set; }
    public string Kind { get; set; } = "DWord";
    public JsonElement Value { get; set; }
}
