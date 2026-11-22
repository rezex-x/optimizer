using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Tweaker.Services;

public sealed class PdhGpuUsageSource : IGpuUsageSource
{
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhMoreData = 0x800007D2;
    private const int ItemSize = 24, StatusOffset = 8, ValueOffset = 16;
    private static readonly IReadOnlyDictionary<int, double> None = new Dictionary<int, double>();

    private readonly object _lock = new();
    private readonly System.Diagnostics.Stopwatch _sinceCollect = new();
    private IntPtr _query, _counter;
    private bool _unavailable, _disposed;

    public IReadOnlyDictionary<int, double> Read()
    {
        lock (_lock)
        {
            try
            {
                if (_unavailable || _disposed) return None;
                if (_query == IntPtr.Zero)
                {
                    if (PdhOpenQueryW(null, IntPtr.Zero, out _query) != 0
                        || PdhAddEnglishCounterW(_query, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out _counter) != 0)
                    {
                        _unavailable = true;
                        Close();
                        return None;
                    }
                    PdhCollectQueryData(_query);
                    _sinceCollect.Restart();
                    return None;
                }

                bool stale = _sinceCollect.Elapsed > TimeSpan.FromSeconds(5);
                if (PdhCollectQueryData(_query) != 0) return None;
                _sinceCollect.Restart();
                if (stale) return None;
                uint size = 0;
                if (PdhGetFormattedCounterArrayW(_counter, PdhFmtDouble, ref size, out uint count, IntPtr.Zero) != PdhMoreData || size == 0)
                    return None;

                IntPtr buffer = Marshal.AllocHGlobal((int)size);
                try
                {
                    if (PdhGetFormattedCounterArrayW(_counter, PdhFmtDouble, ref size, out count, buffer) != 0) return None;
                    var engines = new List<(string Instance, double Utilization)>((int)count);
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr item = buffer + i * ItemSize;
                        string? name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(item));
                        uint status = (uint)Marshal.ReadInt32(item, StatusOffset);
                        double value = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(item, ValueOffset));
                        if (name is not null && status <= 1) engines.Add((name, value));
                    }
                    return GpuEngineParser.PerProcess(engines);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
                return None;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            Close();
        }
    }

    private void Close()
    {
        if (_query == IntPtr.Zero) return;
        PdhCloseQuery(_query);
        _query = IntPtr.Zero;
        _counter = IntPtr.Zero;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string counterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr itemBuffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}
