using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tweaker.Services;

public sealed record PingStat(string Target, double? AvgMs, double? MinMs, double? MaxMs, double? JitterMs, double? LossPct);

public sealed record NetworkResult(PingStat? Internet, PingStat? Gateway)
{
    public static readonly NetworkResult Empty = new(null, null);
}

public interface INetworkDiagnostic
{
    Task<NetworkResult> RunAsync(CancellationToken ct = default);
}

public sealed record DpcResult(double? DpcTimePct, double? InterruptTimePct, double? DpcsQueuedPerSec, double? InterruptsPerSec)
{
    public static readonly DpcResult Empty = new(null, null, null, null);
}

public interface IDpcDiagnostic
{
    Task<DpcResult> SampleAsync(CancellationToken ct = default);
}

public enum DriveKind { Unknown, Ssd, Hdd }
public enum SmartHealth { Unknown, Ok, PredictedFail }

public sealed record DriveHealth(string Model, DriveKind Kind, SmartHealth Smart, double? CapacityGb);
public sealed record VolumeUsage(string Letter, double? FreeGb, double? SizeGb);

public sealed record StorageResult(IReadOnlyList<DriveHealth> Drives, IReadOnlyList<VolumeUsage> Volumes)
{
    public static readonly StorageResult Empty = new(new List<DriveHealth>(), new List<VolumeUsage>());
}

public interface IStorageDiagnostic { StorageResult Read(); }
