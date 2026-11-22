using Tweaker.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeHardwareMonitor : IHardwareMonitor
{
    public HardwareSnapshot Next { get; set; } = HardwareSnapshot.Empty;
    public bool Disposed { get; private set; }
    public HardwareSnapshot Read() => Next;
    public void Dispose() => Disposed = true;
}
