using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tweaker.Services;

public sealed class IpHelperConnectionSource : IConnectionSource
{
    private const int AfInet = 2, AfInet6 = 23;
    private const int TcpTableOwnerPidAll = 5, UdpTableOwnerPid = 1;
    private const uint NoError = 0, ErrorInsufficientBuffer = 122;

    private readonly Stopwatch _sinceSnapshot = new();
    private Dictionary<int, string> _names = new();

    private delegate uint TableCall(byte[]? buffer, ref int size);

    public IReadOnlyList<ConnectionEntry> Read()
    {
        var sockets = new List<RawSocket>();
        try
        {
            if (Tcp(AfInet) is byte[] tcp4) sockets.AddRange(IpTableParser.ParseTcp4(tcp4));
            if (Tcp(AfInet6) is byte[] tcp6) sockets.AddRange(IpTableParser.ParseTcp6(tcp6));
            if (Udp(AfInet) is byte[] udp4) sockets.AddRange(IpTableParser.ParseUdp4(udp4));
            if (Udp(AfInet6) is byte[] udp6) sockets.AddRange(IpTableParser.ParseUdp6(udp6));
        }
        catch { }

        Dictionary<int, string> names = Names(sockets);
        var entries = new List<ConnectionEntry>(sockets.Count);
        foreach (RawSocket s in sockets)
        {
            names.TryGetValue(s.Pid, out string? name);
            entries.Add(new ConnectionEntry(s.Protocol, s.LocalAddress, s.LocalPort, s.RemoteAddress, s.RemotePort,
                s.State, s.Pid, ProcessNames.Display(s.Pid, name)));
        }
        return entries;
    }

    private Dictionary<int, string> Names(List<RawSocket> sockets)
    {
        bool stale = !_sinceSnapshot.IsRunning || _sinceSnapshot.Elapsed > TimeSpan.FromSeconds(5);
        if (stale || sockets.Exists(s => s.Pid > 4 && !_names.ContainsKey(s.Pid)))
        {
            _names = ProcessSnapshot();
            _sinceSnapshot.Restart();
        }
        return _names;
    }

    private static byte[]? Tcp(int family) =>
        Fetch((byte[]? buffer, ref int size) => GetExtendedTcpTable(buffer, ref size, false, family, TcpTableOwnerPidAll, 0));

    private static byte[]? Udp(int family) =>
        Fetch((byte[]? buffer, ref int size) => GetExtendedUdpTable(buffer, ref size, false, family, UdpTableOwnerPid, 0));

    private static byte[]? Fetch(TableCall call)
    {
        try
        {
            int size = 0;
            call(null, ref size);
            for (int attempt = 0; attempt < 4 && size > 0; attempt++)
            {
                var buffer = new byte[size + 4096];
                int capacity = buffer.Length;
                uint result = call(buffer, ref capacity);
                if (result == NoError) return buffer;
                if (result != ErrorInsufficientBuffer) return null;
                size = capacity;
            }
        }
        catch { }
        return null;
    }

    private static Dictionary<int, string> ProcessSnapshot()
    {
        var names = new Dictionary<int, string>();
        try
        {
            foreach (Process p in Process.GetProcesses())
            {
                using (p)
                {
                    try { names[p.Id] = p.ProcessName; }
                    catch { }
                }
            }
        }
        catch { }
        return names;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(byte[]? pTcpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(byte[]? pUdpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved);
}
