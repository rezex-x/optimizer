using System.Text.Json;

namespace Tweaker.Core.Models;

public sealed class BackupEntry
{
    public string Hive { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Name { get; set; }
    public bool Existed { get; set; }
    public string? Kind { get; set; }
    public JsonElement Value { get; set; }
}

public sealed class BackupRecord
{
    public string TweakId { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
    public List<BackupEntry> Entries { get; set; } = new();
}
