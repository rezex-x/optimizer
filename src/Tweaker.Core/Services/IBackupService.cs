using Tweaker.Core.Models;

namespace Tweaker.Core.Services;

public interface IBackupService
{
    BackupRecord Capture(string tweakId, IEnumerable<RegistryOp> ops);
    string Persist(BackupRecord record);
    void Restore(BackupRecord record);
    IReadOnlyList<BackupRecord> LoadAll();
}
