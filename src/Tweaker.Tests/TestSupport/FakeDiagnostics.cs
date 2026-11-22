using System.Threading;
using System.Threading.Tasks;
using Tweaker.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeNetworkDiagnostic : INetworkDiagnostic
{
    public NetworkResult Next { get; set; } = NetworkResult.Empty;
    public Task<NetworkResult> RunAsync(CancellationToken ct = default) => Task.FromResult(Next);
}

public sealed class FakeDpcDiagnostic : IDpcDiagnostic
{
    public DpcResult Next { get; set; } = DpcResult.Empty;
    public Task<DpcResult> SampleAsync(CancellationToken ct = default) => Task.FromResult(Next);
}

public sealed class FakeStorageDiagnostic : IStorageDiagnostic
{
    public StorageResult Next { get; set; } = StorageResult.Empty;
    public StorageResult Read() => Next;
}

public sealed class GatedNetworkDiagnostic : INetworkDiagnostic
{
    public TaskCompletionSource<NetworkResult> Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Calls { get; private set; }
    public Task<NetworkResult> RunAsync(CancellationToken ct = default) { Calls++; return Gate.Task; }
}

public sealed class GatedDpcDiagnostic : IDpcDiagnostic
{
    public TaskCompletionSource<DpcResult> Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Calls { get; private set; }
    public Task<DpcResult> SampleAsync(CancellationToken ct = default) { Calls++; return Gate.Task; }
}
