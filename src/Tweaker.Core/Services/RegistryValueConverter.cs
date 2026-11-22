using System.Text.Json;
using Microsoft.Win32;

namespace Tweaker.Core.Services;

public static class RegistryValueConverter
{
    public static RegistryValueKind ParseKind(string kind) => kind switch
    {
        "DWord" => RegistryValueKind.DWord,
        "QWord" => RegistryValueKind.QWord,
        "String" => RegistryValueKind.String,
        "ExpandString" => RegistryValueKind.ExpandString,
        "MultiString" => RegistryValueKind.MultiString,
        "Binary" => RegistryValueKind.Binary,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown registry kind")
    };

    public static object ToRegistryValue(JsonElement value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => value.GetInt32(),
        RegistryValueKind.QWord => value.GetInt64(),
        RegistryValueKind.String or RegistryValueKind.ExpandString => value.GetString() ?? "",
        RegistryValueKind.MultiString => value.EnumerateArray().Select(e => e.GetString() ?? "").ToArray(),
        RegistryValueKind.Binary => value.EnumerateArray().Select(e => (byte)e.GetInt32()).ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported registry kind")
    };
}
