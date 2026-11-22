using System;
using System.Collections.Generic;
using Tweaker.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeNetworkAdapterSource : INetworkAdapterSource
{
    public NetworkSample Next { get; set; } = NetworkSample.Empty;
    public int Reads { get; private set; }
    public NetworkSample Read() { Reads++; return Next; }
}

public sealed class FakeConnectionSource : IConnectionSource
{
    public IReadOnlyList<ConnectionEntry> Next { get; set; } = Array.Empty<ConnectionEntry>();
    public int Reads { get; private set; }
    public IReadOnlyList<ConnectionEntry> Read() { Reads++; return Next; }
}
