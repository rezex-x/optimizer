using Tweaker.Core.Models;
using Tweaker.Core.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeBackupService : IBackupService
{
    public List<string> Captured { get; } = new();
    public List<BackupRecord> Persisted { get; } = new();

    public BackupRecord Capture(string tweakId, IEnumerable<RegistryOp> ops)
    {
        Captured.Add(tweakId);
        return new BackupRecord { TweakId = tweakId, Timestamp = DateTimeOffset.UtcNow };
    }

    public string Persist(BackupRecord record) { Persisted.Add(record); return "fake"; }
    public void Restore(BackupRecord record) { }
    public IReadOnlyList<BackupRecord> LoadAll() => Persisted;
}
