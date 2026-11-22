using System.Text.Json;
using Microsoft.Win32;
using Tweaker.Core.Json;
using Tweaker.Core.Models;

namespace Tweaker.Core.Services;

public sealed class BackupService : IBackupService
{
    private readonly IRegistryService _registry;
    private readonly string _backupDir;

    public BackupService(IRegistryService registry, string backupDir)
    {
        _registry = registry;
        _backupDir = backupDir;
        Directory.CreateDirectory(_backupDir);
    }

    public BackupRecord Capture(string tweakId, IEnumerable<RegistryOp> ops)
    {
        var record = new BackupRecord { TweakId = tweakId, Timestamp = DateTimeOffset.UtcNow };

        foreach (RegistryOp op in ops)
        {
            (bool exists, RegistryValueKind kind, object? value) = _registry.Read(op.Hive, op.Path, op.Name);
            var entry = new BackupEntry
            {
                Hive = op.Hive,
                Path = op.Path,
                Name = op.Name,
                Existed = exists,
                Kind = exists ? kind.ToString() : null,
                Value = exists
                    ? (value is byte[] bytes ? JsonSerializer.SerializeToElement(Array.ConvertAll(bytes, b => (int)b)) : JsonSerializer.SerializeToElement(value))
                    : JsonSerializer.SerializeToElement((object?)null)
            };
            record.Entries.Add(entry);
        }

        return record;
    }

    public string Persist(BackupRecord record)
    {
        string safeId = string.Concat(record.TweakId.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
        string file = Path.Combine(_backupDir, $"{safeId}-{record.Timestamp:yyyyMMddHHmmssfff}.json");
        File.WriteAllText(file, JsonSerializer.Serialize(record, TweakJson.Options));
        return file;
    }

    public void Restore(BackupRecord record)
    {
        foreach (BackupEntry entry in record.Entries)
        {
            if (entry.Existed && entry.Kind is not null)
            {
                RegistryValueKind kind = RegistryValueConverter.ParseKind(entry.Kind);
                object value = RegistryValueConverter.ToRegistryValue(entry.Value, kind);
                _registry.Set(entry.Hive, entry.Path, entry.Name, kind, value);
            }
            else
            {
                _registry.DeleteValue(entry.Hive, entry.Path, entry.Name);
            }
        }
    }

    public IReadOnlyList<BackupRecord> LoadAll()
    {
        if (!Directory.Exists(_backupDir))
            return Array.Empty<BackupRecord>();

        var records = new List<BackupRecord>();
        foreach (string file in Directory.EnumerateFiles(_backupDir, "*.json"))
        {
            BackupRecord? record = JsonSerializer.Deserialize<BackupRecord>(File.ReadAllText(file), TweakJson.Options);
            if (record is not null)
                records.Add(record);
        }
        return records;
    }
}
