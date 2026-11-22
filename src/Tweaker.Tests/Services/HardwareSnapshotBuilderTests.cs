using System.Collections.Generic;
using System.Linq;
using Tweaker.Services;
using Xunit;
using H = Tweaker.Services.SensorHardware;
using K = Tweaker.Services.SensorKind;

namespace Tweaker.Tests.Services;

public class HardwareSnapshotBuilderTests
{
    private const string Intel = "Intel Core i7-13700K";

    private static SensorReading S(H hw, int id, string hwName, K kind, string name, int index, double? value)
        => new(hw, id, hwName, kind, name, index, value);

    private static List<SensorReading> IntelHybrid(bool withCoreMax = true)
    {
        var list = new List<SensorReading>
        {
            S(H.Cpu, 0, Intel, K.Load, "CPU Total", 0, 25),
            S(H.Cpu, 0, Intel, K.Load, "CPU Core Max", 1, 80),
            S(H.Cpu, 0, Intel, K.Load, "CPU Core #1 Thread #1", 2, 30),
            S(H.Cpu, 0, Intel, K.Load, "CPU Core #1 Thread #2", 3, 10),
            S(H.Cpu, 0, Intel, K.Load, "CPU Core #2", 4, 50),
            S(H.Cpu, 0, Intel, K.Clock, "Bus Speed", 0, 100),
            S(H.Cpu, 0, Intel, K.Clock, "P-Core #1", 1, 4800),
            S(H.Cpu, 0, Intel, K.Clock, "E-Core #1", 2, 3600),
            S(H.Cpu, 0, Intel, K.Temperature, "CPU Package", 0, 61),
            S(H.Cpu, 0, Intel, K.Temperature, "P-Core #1", 1, 70),
            S(H.Cpu, 0, Intel, K.Temperature, "E-Core #1", 2, 58),
            S(H.Cpu, 0, Intel, K.Temperature, "Core Average", 4, 62),
            S(H.Cpu, 0, Intel, K.Temperature, "CPU Core #1 Distance to TjMax", 5, 90),
        };
        if (withCoreMax) list.Add(S(H.Cpu, 0, Intel, K.Temperature, "Core Max", 3, 71));
        return list;
    }

    [Fact]
    public void Intel_hybrid_cpu_values()
    {
        HardwareSnapshot s = HardwareSnapshotBuilder.Build(IntelHybrid());
        Assert.Equal(25.0, s.CpuLoad);
        Assert.Equal(new[] { 30.0, 10.0, 50.0 }, s.CoreLoads);
        Assert.Equal(4200.0, s.CpuClockMhz);
        Assert.Equal(61.0, s.CpuPackageC);
        Assert.Equal(71.0, s.CpuHottestCoreC);
    }

    [Fact]
    public void Hottest_core_falls_back_to_the_highest_per_core_temperature()
        => Assert.Equal(70.0, HardwareSnapshotBuilder.Build(IntelHybrid(withCoreMax: false)).CpuHottestCoreC);

    [Fact]
    public void Core_loads_follow_sensor_index_not_list_order()
    {
        List<SensorReading> reversed = IntelHybrid().AsEnumerable().Reverse().ToList();
        Assert.Equal(new[] { 30.0, 10.0, 50.0 }, HardwareSnapshotBuilder.Build(reversed).CoreLoads);
    }

    [Fact]
    public void Ryzen_uses_tctl_for_the_package_and_averages_the_real_core_clocks()
    {
        const string Amd = "AMD Ryzen 7 5800X3D";
        var sensors = new List<SensorReading>
        {
            S(H.Cpu, 0, Amd, K.Load, "CPU Total", 0, 40),
            S(H.Cpu, 0, Amd, K.Clock, "Bus Speed", 0, 100),
            S(H.Cpu, 0, Amd, K.Clock, "Core #1", 1, 4500),
            S(H.Cpu, 0, Amd, K.Clock, "Core #1 (Effective)", 2, 150),
            S(H.Cpu, 0, Amd, K.Clock, "Core #2", 3, 4300),
            S(H.Cpu, 0, Amd, K.Clock, "Core #2 (Effective)", 4, 90),
            S(H.Cpu, 0, Amd, K.Clock, "Cores (Average)", 5, 4420),
            S(H.Cpu, 0, Amd, K.Clock, "Cores (Average Effective)", 6, 120),
            S(H.Cpu, 0, Amd, K.Temperature, "Core (Tctl/Tdie)", 0, 72),
            S(H.Cpu, 0, Amd, K.Temperature, "CCD1 (Tdie)", 1, 70),
        };
        HardwareSnapshot s = HardwareSnapshotBuilder.Build(sensors);
        Assert.Equal(72.0, s.CpuPackageC);
        Assert.Null(s.CpuHottestCoreC);
        Assert.Equal(4400.0, s.CpuClockMhz);
    }

    [Fact]
    public void Cpu_load_falls_back_to_the_average_core_load()
    {
        var sensors = new List<SensorReading>
        {
            S(H.Cpu, 0, "CPU", K.Load, "CPU Total", 0, null),
            S(H.Cpu, 0, "CPU", K.Load, "CPU Core #1", 1, 20),
            S(H.Cpu, 0, "CPU", K.Load, "CPU Core #2", 2, 40),
        };
        Assert.Equal(30.0, HardwareSnapshotBuilder.Build(sensors).CpuLoad);
    }

    [Fact]
    public void Memory_reads_the_physical_hardware_not_virtual_memory()
    {
        var sensors = new List<SensorReading>
        {
            S(H.Memory, 0, "Virtual Memory", K.Data, "Memory Used", 0, 20),
            S(H.Memory, 0, "Virtual Memory", K.Data, "Memory Available", 1, 30),
            S(H.Memory, 1, "Total Memory", K.Data, "Memory Used", 0, 12.4),
            S(H.Memory, 1, "Total Memory", K.Data, "Memory Available", 1, 19.5),
        };
        HardwareSnapshot s = HardwareSnapshotBuilder.Build(sensors);
        Assert.Equal(12.4, s.MemUsedGb);
        Assert.Equal(31.9, s.MemTotalGb!.Value, 6);
    }

    [Fact]
    public void Older_single_memory_hardware_matches_sensor_names_exactly()
    {
        var sensors = new List<SensorReading>
        {
            S(H.Memory, 0, "Generic Memory", K.Data, "Virtual Memory Used", 0, 20),
            S(H.Memory, 0, "Generic Memory", K.Data, "Memory Used", 1, 8),
            S(H.Memory, 0, "Generic Memory", K.Data, "Memory Available", 2, 8),
        };
        HardwareSnapshot s = HardwareSnapshotBuilder.Build(sensors);
        Assert.Equal(8.0, s.MemUsedGb);
        Assert.Equal(16.0, s.MemTotalGb);
    }

    [Fact]
    public void Gpus_keep_hardware_order_with_vendor_hotspot_and_vram()
    {
        const string Nv = "NVIDIA GeForce RTX 4060 Ti";
        var sensors = new List<SensorReading>
        {
            S(H.GpuIntel, 0, "Intel(R) UHD Graphics 770", K.Load, "D3D 3D", 0, 5),
            S(H.GpuNvidia, 1, Nv, K.Load, "GPU Core", 0, 41),
            S(H.GpuNvidia, 1, Nv, K.Temperature, "GPU Core", 0, 55),
            S(H.GpuNvidia, 1, Nv, K.Temperature, "GPU Hot Spot", 1, 67),
            S(H.GpuNvidia, 1, Nv, K.Clock, "GPU Core", 0, 2505),
            S(H.GpuNvidia, 1, Nv, K.SmallData, "GPU Memory Used", 0, 3100),
            S(H.GpuNvidia, 1, Nv, K.SmallData, "GPU Memory Total", 1, 8188),
        };
        HardwareSnapshot s = HardwareSnapshotBuilder.Build(sensors);
        Assert.Equal(2, s.Gpus.Count);
        Assert.Equal(GpuVendor.Intel, s.Gpus[0].Vendor);
        Assert.Equal(5.0, s.Gpus[0].Load);
        Assert.Null(s.Gpus[0].TempC);
        Assert.Equal(new GpuReading(Nv, GpuVendor.Nvidia, 41, 55, 67, 2505, 3100, 8188), s.Gpus[1]);
    }

    [Fact]
    public void Disk_throughput_sums_all_drives_and_each_drive_reports_its_temperature()
    {
        const string Nvme = "Samsung SSD 980 PRO 1TB", Hdd = "ST2000DM008";
        var sensors = new List<SensorReading>
        {
            S(H.Storage, 0, Nvme, K.Throughput, "Read Rate", 0, 1_048_576),
            S(H.Storage, 0, Nvme, K.Throughput, "Write Rate", 1, 524_288),
            S(H.Storage, 0, Nvme, K.Temperature, "Temperature #2", 1, 50),
            S(H.Storage, 0, Nvme, K.Temperature, "Composite Temperature", 0, 42),
            S(H.Storage, 1, Hdd, K.Throughput, "Read Rate", 0, 0),
            S(H.Storage, 1, Hdd, K.Throughput, "Write Rate", 1, 0),
            S(H.Storage, 1, Hdd, K.Temperature, "Temperature", 0, 35),
            S(H.Storage, 2, "USB DISK", K.Load, "Used Space", 0, 50),
        };
        HardwareSnapshot s = HardwareSnapshotBuilder.Build(sensors);
        Assert.Equal(1_572_864.0, s.DiskBytesPerSec);
        Assert.Equal(new[] { new DriveTemp(Nvme, 42), new DriveTemp(Hdd, 35) }, s.DriveTemps);
    }

    [Fact]
    public void Each_storage_item_gets_a_disk_reading()
    {
        const string Nvme = "Samsung SSD 980 PRO 1TB", Hdd = "ST2000DM008";
        var sensors = new List<SensorReading>
        {
            S(H.Storage, 0, Nvme, K.Load, "Total Activity", 0, 12),
            S(H.Storage, 0, Nvme, K.Throughput, "Read Rate", 1, 1_048_576),
            S(H.Storage, 0, Nvme, K.Throughput, "Write Rate", 2, 524_288),
            S(H.Storage, 0, Nvme, K.Load, "Used Space", 3, 63),
            S(H.Storage, 0, Nvme, K.Temperature, "Composite Temperature", 4, 42),
            S(H.Storage, 1, Hdd, K.Throughput, "Read Rate", 0, 0),
            S(H.Storage, 1, Hdd, K.Throughput, "Write Rate", 1, 0),
            S(H.Storage, 1, Hdd, K.Load, "Used Space", 2, 80),
            S(H.Storage, 1, Hdd, K.Temperature, "Temperature", 3, 35),
        };
        HardwareSnapshot s = HardwareSnapshotBuilder.Build(sensors);
        Assert.Equal(new[]
        {
            new DiskReading(Nvme, 12, 1_048_576, 524_288, 63, 42),
            new DiskReading(Hdd, null, 0, 0, 80, 35),
        }, s.Disks);
    }

    [Fact]
    public void Disk_throughput_is_unknown_without_rate_sensors()
    {
        var sensors = new List<SensorReading> { S(H.Storage, 0, "Disk", K.Load, "Used Space", 0, 50) };
        Assert.Null(HardwareSnapshotBuilder.Build(sensors).DiskBytesPerSec);
    }

    [Fact]
    public void Network_reports_the_busiest_adapter()
    {
        var sensors = new List<SensorReading>
        {
            S(H.Network, 0, "vEthernet (WSL)", K.Throughput, "Upload Speed", 0, 0),
            S(H.Network, 0, "vEthernet (WSL)", K.Throughput, "Download Speed", 1, 0),
            S(H.Network, 1, "Ethernet", K.Throughput, "Upload Speed", 0, 100_000),
            S(H.Network, 1, "Ethernet", K.Throughput, "Download Speed", 1, 2_000_000),
            S(H.Network, 2, "Wi-Fi", K.Load, "Network Utilization", 0, 0),
        };
        HardwareSnapshot s = HardwareSnapshotBuilder.Build(sensors);
        Assert.Equal(2_100_000.0, s.NetBytesPerSec);
        Assert.Equal("Ethernet", s.NetAdapter);
    }

    [Fact]
    public void An_idle_network_prefers_a_physical_adapter_over_a_virtual_one()
    {
        var sensors = new List<SensorReading>
        {
            S(H.Network, 0, "vEthernet (WSL)", K.Throughput, "Download Speed", 0, 0),
            S(H.Network, 1, "Ethernet", K.Throughput, "Download Speed", 0, 0),
        };
        HardwareSnapshot s = HardwareSnapshotBuilder.Build(sensors);
        Assert.Equal(0.0, s.NetBytesPerSec);
        Assert.Equal("Ethernet", s.NetAdapter);
    }

    [Fact]
    public void No_sensors_gives_an_empty_snapshot()
    {
        HardwareSnapshot s = HardwareSnapshotBuilder.Build(new List<SensorReading>());
        Assert.Null(s.CpuLoad);
        Assert.Empty(s.CoreLoads);
        Assert.Null(s.CpuClockMhz);
        Assert.Null(s.CpuPackageC);
        Assert.Null(s.MemUsedGb);
        Assert.Null(s.MemTotalGb);
        Assert.Null(s.DiskBytesPerSec);
        Assert.Null(s.NetBytesPerSec);
        Assert.Null(s.NetAdapter);
        Assert.Empty(s.Gpus);
        Assert.Empty(s.DriveTemps);
        Assert.Empty(s.Disks);
    }

    [Fact]
    public void Drives_without_a_real_temperature_get_no_row()
    {
        var sensors = new List<SensorReading>
        {
            S(H.Storage, 0, "Msft Virtual Disk", K.Temperature, "Temperature", 0, 0),
            S(H.Storage, 1, "NVMe A", K.Temperature, "Composite Temperature", 0, 0),
            S(H.Storage, 1, "NVMe A", K.Temperature, "Warning Temperature", 1, 84),
            S(H.Storage, 1, "NVMe A", K.Temperature, "Critical Temperature", 2, 85),
            S(H.Storage, 2, "NVMe B", K.Temperature, "Composite Temperature", 0, 0),
            S(H.Storage, 2, "NVMe B", K.Temperature, "Warning Temperature", 1, 84),
            S(H.Storage, 2, "NVMe B", K.Temperature, "Temperature #1", 2, 47),
        };
        Assert.Equal(new[] { new DriveTemp("NVMe B", 47) }, HardwareSnapshotBuilder.Build(sensors).DriveTemps);
    }

    [Fact]
    public void An_idle_network_names_the_adapter_that_has_moved_the_most_data()
    {
        var sensors = new List<SensorReading>
        {
            S(H.Network, 0, "Ethernet", K.Throughput, "Download Speed", 0, 0),
            S(H.Network, 0, "Ethernet", K.Data, "Data Downloaded", 1, 0),
            S(H.Network, 1, "Wi-Fi", K.Throughput, "Download Speed", 0, 0),
            S(H.Network, 1, "Wi-Fi", K.Data, "Data Downloaded", 1, 12.5),
            S(H.Network, 1, "Wi-Fi", K.Data, "Data Uploaded", 2, 1.2),
            S(H.Network, 2, "Local Area Connection* 2", K.Throughput, "Download Speed", 0, 0),
        };
        Assert.Equal("Wi-Fi", HardwareSnapshotBuilder.Build(sensors).NetAdapter);
    }
}
