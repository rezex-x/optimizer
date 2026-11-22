using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class PerformanceViewModelTests
{
    private static DpcSampleViewModel Dpc() => new(new FakeDpcDiagnostic());

    private static PerformanceViewModel Build(IHardwareMonitor monitor) =>
        new(monitor, new FakeSystemInfoSource(), Dpc());

    private static HardwareSnapshot Busy() => HardwareSnapshot.Empty with
    {
        CpuLoad = 24.4,
        CpuClockMhz = 4620,
        MemUsedGb = 12.4,
        MemTotalGb = 31.9,
        DiskBytesPerSec = 5 * MetricFormat.BytesPerMiB,
        NetBytesPerSec = 2 * MetricFormat.BytesPerMiB,
        NetAdapter = "Ethernet",
        Gpus = new[] { new GpuReading("NVIDIA GeForce RTX 4060 Ti", GpuVendor.Nvidia, 41, 55, 67, 2505, 3100, 8188) },
    };

    [Fact]
    public async Task Refresh_fills_the_five_cards()
    {
        var vm = Build(new FakeHardwareMonitor { Next = Busy() });

        await vm.RefreshAsync();

        Assert.Equal("24", vm.Cpu.ValueText);
        Assert.Equal("4.62 GHz", vm.Cpu.SubText);
        Assert.Equal(0.244, vm.Cpu.Fraction, 6);
        Assert.Equal("12", vm.Memory.ValueText);
        Assert.Equal("of 32 GB", vm.Memory.SubText);
        Assert.Equal(31.9, vm.Memory.Maximum, 6);
        Assert.Equal(12.4 / 31.9, vm.Memory.Fraction, 6);
        Assert.Equal("5.0", vm.Disk.ValueText);
        Assert.Equal("all drives", vm.Disk.SubText);
        Assert.Equal("2.0", vm.Network.ValueText);
        Assert.Equal("Ethernet", vm.Network.SubText);
        Assert.Equal("41", vm.Gpu.ValueText);
        Assert.Equal("NVIDIA GeForce RTX 4060 Ti", vm.Gpu.SubText);
    }

    [Fact]
    public async Task Each_refresh_adds_one_history_sample_up_to_forty()
    {
        var monitor = new FakeHardwareMonitor();
        var vm = Build(monitor);

        for (int i = 1; i <= 45; i++)
        {
            monitor.Next = HardwareSnapshot.Empty with { CpuLoad = i };
            await vm.RefreshAsync();
        }

        Assert.Equal(40, vm.Cpu.History.Length);
        Assert.Equal(6, vm.Cpu.History[0]);
        Assert.Equal(45, vm.Cpu.History[^1]);
    }

    [Fact]
    public async Task Before_any_successful_read_the_cards_show_dashes_and_empty_states()
    {
        var vm = Build(new FakeHardwareMonitor());

        await vm.RefreshAsync();

        Assert.Equal("—", vm.Cpu.ValueText);
        Assert.Equal("—", vm.Memory.ValueText);
        Assert.Equal("—", vm.Disk.ValueText);
        Assert.Equal("—", vm.Network.ValueText);
        Assert.Equal("—", vm.Gpu.ValueText);
        Assert.Equal(0, vm.Cpu.Fraction);
        Assert.Empty(vm.Cpu.History);
        Assert.Equal("", vm.Network.SubText);
        Assert.False(vm.HasCores);
        Assert.Equal("CPU CORES", vm.CoresHeader);
        Assert.False(vm.HasTemps);
    }

    [Fact]
    public async Task A_failed_read_keeps_the_last_values_instead_of_blanking_the_cards()
    {
        var monitor = new FakeHardwareMonitor
        {
            Next = HardwareSnapshot.Empty with { CpuLoad = 40, CoreLoads = new[] { 10.0, 20.0 }, CpuPackageC = 55 },
        };
        var vm = Build(monitor);
        await vm.RefreshAsync();

        monitor.Next = HardwareSnapshot.Empty;
        await vm.RefreshAsync();

        Assert.Equal("40", vm.Cpu.ValueText);
        Assert.Single(vm.Cpu.History);
        Assert.Equal(2, vm.Cores.Count);
        Assert.True(vm.HasTemps);
    }

    [Fact]
    public async Task A_successful_read_without_sensors_clears_the_rows()
    {
        var monitor = new FakeHardwareMonitor
        {
            Next = HardwareSnapshot.Empty with { CpuLoad = 40, CoreLoads = new[] { 10.0, 20.0 }, CpuPackageC = 55 },
        };
        var vm = Build(monitor);
        await vm.RefreshAsync();

        monitor.Next = HardwareSnapshot.Empty with { };
        await vm.RefreshAsync();

        Assert.Equal("—", vm.Cpu.ValueText);
        Assert.False(vm.HasCores);
        Assert.False(vm.HasTemps);
    }

    [Fact]
    public async Task Cores_get_one_row_per_logical_processor_and_update_in_place()
    {
        var monitor = new FakeHardwareMonitor
        {
            Next = HardwareSnapshot.Empty with { CoreLoads = Enumerable.Range(0, 16).Select(i => i * 6.0).ToArray() },
        };
        var vm = Build(monitor);

        await vm.RefreshAsync();

        Assert.Equal(16, vm.Cores.Count);
        Assert.Equal("Core 0", vm.Cores[0].Label);
        Assert.Equal("Core 15", vm.Cores[15].Label);
        Assert.Equal("CPU CORES (16)", vm.CoresHeader);
        Assert.True(vm.HasCores);
        Assert.Equal("90%", vm.Cores[15].LoadText);
        Assert.True(vm.Cores[15].IsHigh);
        Assert.False(vm.Cores[13].IsHigh);

        CoreLoadViewModel first = vm.Cores[0];
        monitor.Next = monitor.Next with { CoreLoads = Enumerable.Repeat(50.0, 16).ToArray() };
        await vm.RefreshAsync();

        Assert.Same(first, vm.Cores[0]);
        Assert.Equal("50%", vm.Cores[0].LoadText);
    }

    [Fact]
    public async Task Temperatures_list_only_known_sensors_with_levels()
    {
        var monitor = new FakeHardwareMonitor
        {
            Next = HardwareSnapshot.Empty with
            {
                CpuPackageC = 64.9,
                Gpus = new[] { new GpuReading("RTX", GpuVendor.Nvidia, 10, 65, 80, null, null, null) },
                DriveTemps = new[] { new DriveTemp("Samsung SSD 980 PRO 1TB", 42) },
            },
        };
        var vm = Build(monitor);

        await vm.RefreshAsync();

        Assert.Equal(new[] { "CPU package", "GPU core", "GPU hotspot", "Samsung SSD 980 PRO 1TB" },
            vm.Temps.Select(t => t.Label));
        Assert.True(vm.HasTemps);
        Assert.False(vm.Temps[0].IsWarm);
        Assert.False(vm.Temps[0].IsHot);
        Assert.True(vm.Temps[1].IsWarm);
        Assert.True(vm.Temps[2].IsHot);
        Assert.False(vm.Temps[2].IsWarm);
        Assert.Equal("65°C", vm.Temps[1].Text);
        Assert.Equal(42, vm.Temps[3].BarValue);
        Assert.True(vm.Temps[3].IsLast);
        Assert.False(vm.Temps[0].IsLast);
    }

    [Fact]
    public async Task The_gpu_card_prefers_nvidia_or_amd_over_intel()
    {
        var monitor = new FakeHardwareMonitor
        {
            Next = HardwareSnapshot.Empty with
            {
                Gpus = new[]
                {
                    new GpuReading("Intel(R) UHD Graphics 770", GpuVendor.Intel, 5, null, null, null, null, null),
                    new GpuReading("AMD Radeon RX 7800 XT", GpuVendor.Amd, 63, 60, 75, null, null, null),
                },
            },
        };
        var vm = Build(monitor);

        await vm.RefreshAsync();

        Assert.Equal("63", vm.Gpu.ValueText);
        Assert.Equal("AMD Radeon RX 7800 XT", vm.Gpu.SubText);
        Assert.Equal("60°C", vm.Temps.Single(t => t.Label == "GPU core").Text);
    }

    [Fact]
    public void System_rows_start_as_dashes_in_a_fixed_order()
    {
        var vm = Build(new FakeHardwareMonitor());
        Assert.Equal(new[] { "CPU", "Cores / threads", "Memory", "GPU", "Motherboard", "OS", "Uptime", "Processes" },
            vm.SystemRows.Select(r => r.Key));
        Assert.All(vm.SystemRows, r => Assert.Equal("—", r.Value));
        Assert.True(vm.SystemRows[^1].IsLast);
        Assert.True(vm.SystemRows.Single(r => r.Key == "Uptime").IsMono);
    }

    [Fact]
    public async Task System_facts_load_once_and_feed_the_cpu_sub_line()
    {
        const ulong GiB = 1UL << 30;
        var system = new FakeSystemInfoSource
        {
            Info = new SystemInfo("AMD Ryzen 7 5800X3D 8-Core Processor", 3400, 8, 16,
                new[] { new MemoryModule(16 * GiB, 3600, 26), new MemoryModule(16 * GiB, 3600, 26) },
                new[] { "NVIDIA GeForce RTX 4060 Ti" },
                "Micro-Star International Co., Ltd.", "MAG B550 TOMAHAWK (MS-7C91)",
                "Windows 10 Pro", "23H2", 22631),
            Processes = 214,
            UptimeValue = new TimeSpan(2, 14, 32, 0),
        };
        var monitor = new FakeHardwareMonitor { Next = HardwareSnapshot.Empty with { CpuClockMhz = 4620 } };
        var vm = new PerformanceViewModel(monitor, system, Dpc());

        await vm.LoadSystemInfoAsync();
        await vm.LoadSystemInfoAsync();
        await vm.RefreshAsync();

        Assert.Equal(1, system.ReadCalls);
        string Row(string key) => vm.SystemRows.Single(r => r.Key == key).Value;
        Assert.Equal("AMD Ryzen 7 5800X3D 8-Core Processor @ 3.4 GHz", Row("CPU"));
        Assert.Equal("8 / 16", Row("Cores / threads"));
        Assert.Equal("32 GB DDR4-3600 (2 × 16 GB)", Row("Memory"));
        Assert.Equal("NVIDIA GeForce RTX 4060 Ti", Row("GPU"));
        Assert.Equal("MSI MAG B550 TOMAHAWK (MS-7C91)", Row("Motherboard"));
        Assert.Equal("Windows 11 Pro 23H2 (22631)", Row("OS"));
        Assert.Equal("2d 14h 32m", Row("Uptime"));
        Assert.Equal("214 running", Row("Processes"));
        Assert.Equal("4.62 GHz · 8C / 16T", vm.Cpu.SubText);
    }

    [Fact]
    public async Task A_tick_during_a_running_read_is_skipped()
    {
        var monitor = new GatedMonitor();
        var vm = Build(monitor);

        Task first = vm.RefreshAsync();
        Assert.True(monitor.Entered.Wait(TimeSpan.FromSeconds(5)));

        await vm.RefreshAsync();
        Assert.Equal(1, monitor.Reads);

        monitor.Release.Set();
        await first;
        Assert.Equal(1, monitor.Reads);
    }

    [Fact]
    public async Task A_failing_read_shows_dashes_instead_of_throwing()
    {
        var vm = Build(new ThrowingMonitor());
        await vm.RefreshAsync();
        Assert.Equal("—", vm.Cpu.ValueText);
        Assert.Equal("—", vm.SystemRows.Single(r => r.Key == "Uptime").Value);
    }

    [Fact]
    public void Dispose_disposes_the_monitor()
    {
        var monitor = new FakeHardwareMonitor();
        var vm = Build(monitor);
        vm.Dispose();
        Assert.True(monitor.Disposed);
    }

    [Fact]
    public async Task Disks_get_rows_with_the_activity_rate_used_space_and_temperature_texts()
    {
        var monitor = new FakeHardwareMonitor
        {
            Next = HardwareSnapshot.Empty with
            {
                Disks = new[]
                {
                    new DiskReading("Samsung SSD 980 PRO 1TB", 37, 12.3 * MetricFormat.BytesPerMiB,
                        0.4 * MetricFormat.BytesPerMiB, 63, 41),
                },
            },
        };
        var vm = new PerformanceViewModel(monitor, new FakeSystemInfoSource(), Dpc(), new FakeVolumeSource());

        await vm.RefreshAsync();

        Assert.Single(vm.Disks);
        DiskRowViewModel row = vm.Disks[0];
        Assert.Equal("Samsung SSD 980 PRO 1TB", row.Name);
        Assert.Equal("37 %", row.ActivityText);
        Assert.Equal(0.37, row.ActivityFraction, 6);
        Assert.Equal("R 12.3 MB/s · W 0.4 MB/s", row.RatesText);
        Assert.Equal("Used 63 %", row.UsedText);
        Assert.Equal("41 °C", row.TempText);
        Assert.True(vm.HasDisks);
    }

    [Fact]
    public async Task A_disk_missing_activity_rates_and_used_space_shows_dashes_and_blanks()
    {
        var monitor = new FakeHardwareMonitor
        {
            Next = HardwareSnapshot.Empty with
            {
                Disks = new[] { new DiskReading("Unknown Drive", null, null, null, null, null) },
            },
        };
        var vm = Build(monitor);

        await vm.RefreshAsync();

        DiskRowViewModel row = vm.Disks[0];
        Assert.Equal("—", row.ActivityText);
        Assert.Equal(0, row.ActivityFraction);
        Assert.Equal("R — · W —", row.RatesText);
        Assert.Equal("", row.UsedText);
        Assert.Equal("", row.TempText);
    }

    [Fact]
    public async Task A_second_tick_reuses_the_same_disk_row_instance()
    {
        var monitor = new FakeHardwareMonitor
        {
            Next = HardwareSnapshot.Empty with
            {
                Disks = new[] { new DiskReading("NVMe A", 10, null, null, null, null) },
            },
        };
        var vm = Build(monitor);
        await vm.RefreshAsync();
        DiskRowViewModel first = vm.Disks[0];

        monitor.Next = monitor.Next with
        {
            Disks = new[] { new DiskReading("NVMe A", 90, null, null, null, null) },
        };
        await vm.RefreshAsync();

        Assert.Same(first, vm.Disks[0]);
        Assert.Equal("90 %", vm.Disks[0].ActivityText);
    }

    [Fact]
    public async Task Volumes_get_a_row_per_ready_fixed_drive_on_the_first_tick()
    {
        const long GiB = 1L << 30;
        var volumes = new FakeVolumeSource
        {
            Next = new[] { new VolumeInfo("C:", "Windows", 476 * GiB, 175 * GiB) },
        };
        var vm = new PerformanceViewModel(new FakeHardwareMonitor(), new FakeSystemInfoSource(), Dpc(), volumes);

        await vm.RefreshAsync();
        await WaitUntilAsync(() => vm.Volumes.Count > 0);

        Assert.Single(vm.Volumes);
        Assert.Equal("C:  Windows · 301 of 476 GB used", vm.Volumes[0].Text);
        Assert.True(vm.HasDisks);
    }

    [Fact]
    public async Task A_volume_with_no_label_omits_it()
    {
        const long GiB = 1L << 30;
        var volumes = new FakeVolumeSource
        {
            Next = new[] { new VolumeInfo("D:", "", 476 * GiB, 175 * GiB) },
        };
        var vm = new PerformanceViewModel(new FakeHardwareMonitor(), new FakeSystemInfoSource(), Dpc(), volumes);

        await vm.RefreshAsync();
        await WaitUntilAsync(() => vm.Volumes.Count > 0);

        Assert.Equal("D:  301 of 476 GB used", vm.Volumes[0].Text);
    }

    [Fact]
    public async Task Volumes_refresh_only_on_the_first_tick_and_every_fifth_tick()
    {
        const long GiB = 1L << 30;
        var volumes = new FakeVolumeSource { Next = new[] { new VolumeInfo("C:", "", 100 * GiB, 50 * GiB) } };
        var vm = new PerformanceViewModel(new FakeHardwareMonitor(), new FakeSystemInfoSource(), Dpc(), volumes);

        await vm.RefreshAsync();
        await WaitUntilAsync(() => volumes.ReadCalls >= 1);
        Assert.Equal(1, volumes.ReadCalls);

        await vm.RefreshAsync();
        await vm.RefreshAsync();
        await vm.RefreshAsync();
        await Task.Delay(50);
        Assert.Equal(1, volumes.ReadCalls);

        await vm.RefreshAsync();
        await WaitUntilAsync(() => volumes.ReadCalls >= 2);
        Assert.Equal(2, volumes.ReadCalls);
    }

    [Fact]
    public async Task Without_volumes_only_the_last_disk_row_drops_its_divider()
    {
        var monitor = new FakeHardwareMonitor
        {
            Next = HardwareSnapshot.Empty with
            {
                Disks = new[] { new DiskReading("NVMe A", 10, null, null, null, null), new DiskReading("NVMe B", 20, null, null, null, null) },
            },
        };
        var volumes = new FakeVolumeSource();
        var vm = new PerformanceViewModel(monitor, new FakeSystemInfoSource(), Dpc(), volumes);

        await vm.RefreshAsync();
        await WaitUntilAsync(() => volumes.ReadCalls >= 1);

        Assert.Equal(new[] { false, true }, vm.Disks.Select(d => d.IsLast));
    }

    [Fact]
    public async Task With_volumes_only_the_last_volume_row_drops_its_divider()
    {
        const long GiB = 1L << 30;
        var monitor = new FakeHardwareMonitor
        {
            Next = HardwareSnapshot.Empty with
            {
                Disks = new[] { new DiskReading("NVMe A", 10, null, null, null, null), new DiskReading("NVMe B", 20, null, null, null, null) },
            },
        };
        var volumes = new FakeVolumeSource
        {
            Next = new[] { new VolumeInfo("C:", "", 100 * GiB, 50 * GiB), new VolumeInfo("D:", "", 100 * GiB, 50 * GiB) },
        };
        var vm = new PerformanceViewModel(monitor, new FakeSystemInfoSource(), Dpc(), volumes);

        await vm.RefreshAsync();
        await WaitUntilAsync(() => vm.Volumes.Count == 2 && vm.Volumes[1].IsLast);

        Assert.Equal(new[] { false, true }, vm.Volumes.Select(v => v.IsLast));
        Assert.All(vm.Disks, d => Assert.False(d.IsLast));

        await vm.RefreshAsync();
        Assert.All(vm.Disks, d => Assert.False(d.IsLast));
        Assert.True(vm.Volumes[1].IsLast);
    }

    [Fact]
    public async Task Volume_read_runs_off_the_ui_thread_and_does_not_block_the_tick()
    {
        var gate = new GatedVolumeSource();
        var monitor = new FakeHardwareMonitor
        {
            Next = HardwareSnapshot.Empty with { Disks = new[] { new DiskReading("NVMe A", 10, null, null, null, null) } },
        };
        var vm = new PerformanceViewModel(monitor, new FakeSystemInfoSource(), Dpc(), gate);

        await vm.RefreshAsync();
        Assert.True(gate.Entered.Wait(TimeSpan.FromSeconds(5)));

        Assert.Single(vm.Disks);
        Assert.Empty(vm.Volumes);

        gate.Release.Set();
        await WaitUntilAsync(() => vm.Volumes.Count > 0);
        Assert.Single(vm.Volumes);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int maxAttempts = 200, int delayMs = 10)
    {
        for (int i = 0; i < maxAttempts && !condition(); i++) await Task.Delay(delayMs);
    }

    private sealed class FakeVolumeSource : IVolumeSource
    {
        public IReadOnlyList<VolumeInfo> Next { get; set; } = Array.Empty<VolumeInfo>();
        public int ReadCalls { get; private set; }

        public IReadOnlyList<VolumeInfo> Read()
        {
            ReadCalls++;
            return Next;
        }
    }

    private sealed class GatedVolumeSource : IVolumeSource
    {
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);

        public IReadOnlyList<VolumeInfo> Read()
        {
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(10));
            return new[] { new VolumeInfo("C:", "", 100L << 30, 50L << 30) };
        }
    }

    private sealed class GatedMonitor : IHardwareMonitor
    {
        private int _reads;
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public int Reads => Volatile.Read(ref _reads);

        public HardwareSnapshot Read()
        {
            Interlocked.Increment(ref _reads);
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(10));
            return HardwareSnapshot.Empty;
        }

        public void Dispose() { }
    }

    private sealed class ThrowingMonitor : IHardwareMonitor
    {
        public HardwareSnapshot Read() => throw new InvalidOperationException("sensor driver blew up");
        public void Dispose() { }
    }
}
