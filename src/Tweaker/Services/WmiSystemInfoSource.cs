using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Tweaker.Services;

public sealed class WmiSystemInfoSource : ISystemInfoSource
{
    public SystemInfo Read()
    {
        string? cpuName = null;
        double? baseMhz = null;
        try
        {
            using RegistryKey? k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            cpuName = (k?.GetValue("ProcessorNameString") as string)?.Trim();
            if (k?.GetValue("~MHz") is int mhz && mhz > 0) baseMhz = mhz;
        }
        catch { }

        int cores = 0, threads = 0;
        Query("SELECT NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor", mo =>
        {
            cores += ToInt(mo["NumberOfCores"]);
            threads += ToInt(mo["NumberOfLogicalProcessors"]);
        });

        var modules = new List<MemoryModule>();
        void AddModule(ManagementObject mo, bool extended)
        {
            ulong capacity = ToULong(mo["Capacity"]);
            if (capacity == 0) return;
            int speed = extended ? ToInt(mo["ConfiguredClockSpeed"]) : 0;
            if (speed <= 0) speed = ToInt(mo["Speed"]);
            modules.Add(new MemoryModule(capacity, speed > 0 ? speed : null, extended ? ToInt(mo["SMBIOSMemoryType"]) : 0));
        }
        if (!Query("SELECT Capacity, ConfiguredClockSpeed, Speed, SMBIOSMemoryType FROM Win32_PhysicalMemory", mo => AddModule(mo, true)))
        {
            modules.Clear();
            Query("SELECT Capacity, Speed FROM Win32_PhysicalMemory", mo => AddModule(mo, false));
        }

        var gpus = new List<string>();
        void AddGpu(ManagementObject mo)
        {
            string? name = mo["Name"]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(name)) gpus.Add(name);
        }
        Query(@"SELECT Name FROM Win32_VideoController WHERE PNPDeviceID LIKE 'PCI\\%'", AddGpu);
        if (gpus.Count == 0) Query("SELECT Name FROM Win32_VideoController", AddGpu);

        string? boardMaker = null, boardProduct = null;
        Query("SELECT Manufacturer, Product FROM Win32_BaseBoard", mo =>
        {
            boardMaker ??= mo["Manufacturer"]?.ToString();
            boardProduct ??= mo["Product"]?.ToString();
        });

        string? osName = null, osVersion = null;
        int? osBuild = null;
        try
        {
            using RegistryKey? k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            osName = k?.GetValue("ProductName") as string;
            osVersion = (k?.GetValue("DisplayVersion") as string) ?? (k?.GetValue("ReleaseId") as string);
            if (int.TryParse(k?.GetValue("CurrentBuildNumber") as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out int b))
                osBuild = b;
        }
        catch { }

        return new SystemInfo(cpuName, baseMhz,
            cores > 0 ? cores : null,
            threads > 0 ? threads : Environment.ProcessorCount,
            modules, gpus, boardMaker, boardProduct, osName, osVersion, osBuild);
    }

    public int? ProcessCount()
    {
        try
        {
            return GetPerformanceInfo(out PerformanceInformation info, Marshal.SizeOf<PerformanceInformation>())
                ? info.ProcessCount
                : null;
        }
        catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        public int Size;
        public IntPtr CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable, SystemCache,
            KernelTotal, KernelPaged, KernelNonPaged, PageSize;
        public int HandleCount, ProcessCount, ThreadCount;
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(out PerformanceInformation info, int size);

    public TimeSpan Uptime() => TimeSpan.FromMilliseconds(Environment.TickCount64);

    private static bool Query(string wql, Action<ManagementObject> each)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            using ManagementObjectCollection results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                using (mo) each(mo);
            }
            return true;
        }
        catch { return false; }
    }

    private static int ToInt(object? v)
    {
        try { return v is null ? 0 : Convert.ToInt32(v, CultureInfo.InvariantCulture); }
        catch { return 0; }
    }

    private static ulong ToULong(object? v)
    {
        try { return v is null ? 0 : Convert.ToUInt64(v, CultureInfo.InvariantCulture); }
        catch { return 0; }
    }
}
