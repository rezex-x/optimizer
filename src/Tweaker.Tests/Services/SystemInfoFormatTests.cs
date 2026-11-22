using System;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class SystemInfoFormatTests
{
    private const ulong GiB = 1UL << 30;

    [Fact]
    public void Cpu_appends_the_base_clock_unless_the_name_already_has_one()
    {
        Assert.Equal("AMD Ryzen 7 5800X3D 8-Core Processor @ 3.4 GHz",
            SystemInfoFormat.Cpu("AMD Ryzen 7 5800X3D 8-Core Processor           ", 3400));
        Assert.Equal("Intel(R) Core(TM) i7-8700K CPU @ 3.70GHz",
            SystemInfoFormat.Cpu("Intel(R) Core(TM) i7-8700K CPU @ 3.70GHz", 3696));
        Assert.Equal("Some CPU", SystemInfoFormat.Cpu("Some   CPU", null));
        Assert.Equal("—", SystemInfoFormat.Cpu(null, 3400));
        Assert.Equal("—", SystemInfoFormat.Cpu("   ", 3400));
    }

    [Fact]
    public void Cores_and_threads()
    {
        Assert.Equal("8 / 16", SystemInfoFormat.CoresThreads(8, 16));
        Assert.Equal("— / 16", SystemInfoFormat.CoresThreads(null, 16));
        Assert.Equal("—", SystemInfoFormat.CoresThreads(null, null));
    }

    [Fact]
    public void Memory_describes_total_type_speed_and_layout()
    {
        Assert.Equal("32 GB DDR4-3600 (2 × 16 GB)", SystemInfoFormat.Memory(new[]
        {
            new MemoryModule(16 * GiB, 3600, 26), new MemoryModule(16 * GiB, 3600, 26),
        }));
        Assert.Equal("24 GB DDR5 (16 GB + 8 GB)", SystemInfoFormat.Memory(new[]
        {
            new MemoryModule(8 * GiB, null, 34), new MemoryModule(16 * GiB, null, 34),
        }));
        Assert.Equal("8 GB 2400 MT/s (1 × 8 GB)", SystemInfoFormat.Memory(new[] { new MemoryModule(8 * GiB, 2400, 0) }));
        Assert.Equal("4 GB (1 × 4 GB)", SystemInfoFormat.Memory(new[] { new MemoryModule(4 * GiB, null, 0) }));
        Assert.Equal("—", SystemInfoFormat.Memory(Array.Empty<MemoryModule>()));
    }

    [Fact]
    public void Memory_type_codes()
    {
        Assert.Equal("DDR4", SystemInfoFormat.MemoryType(26));
        Assert.Equal("DDR5", SystemInfoFormat.MemoryType(34));
        Assert.Equal("LPDDR5", SystemInfoFormat.MemoryType(35));
        Assert.Null(SystemInfoFormat.MemoryType(0));
    }

    [Fact]
    public void Board_shortens_known_vendors_and_drops_oem_placeholders()
    {
        Assert.Equal("MSI MAG B550 TOMAHAWK (MS-7C91)",
            SystemInfoFormat.Board("Micro-Star International Co., Ltd.", "MAG B550 TOMAHAWK (MS-7C91)"));
        Assert.Equal("ASUS ROG STRIX B650E-F GAMING WIFI",
            SystemInfoFormat.Board("ASUSTeK COMPUTER INC.", "ROG STRIX B650E-F GAMING WIFI"));
        Assert.Equal("—", SystemInfoFormat.Board("To be filled by O.E.M.", "Default string"));
        Assert.Equal("Contoso", SystemInfoFormat.Board("Contoso", null));
        Assert.Equal("Z790 AORUS", SystemInfoFormat.Board(null, "Z790 AORUS"));
    }

    [Fact]
    public void Os_names_windows_11_by_build_number()
    {
        Assert.Equal("Windows 11 Pro 23H2 (22631)", SystemInfoFormat.Os("Windows 10 Pro", "23H2", 22631));
        Assert.Equal("Windows 10 Pro 22H2 (19045)", SystemInfoFormat.Os("Windows 10 Pro", "22H2", 19045));
        Assert.Equal("Windows 11 Home (22000)", SystemInfoFormat.Os("Windows 10 Home", null, 22000));
        Assert.Equal("—", SystemInfoFormat.Os(null, "23H2", 22631));
    }

    [Fact]
    public void Uptime_and_processes()
    {
        Assert.Equal("2d 14h 32m", SystemInfoFormat.Uptime(new TimeSpan(2, 14, 32, 10)));
        Assert.Equal("3h 5m", SystemInfoFormat.Uptime(new TimeSpan(3, 5, 0)));
        Assert.Equal("42m", SystemInfoFormat.Uptime(TimeSpan.FromMinutes(42)));
        Assert.Equal("214 running", SystemInfoFormat.Processes(214));
        Assert.Equal("—", SystemInfoFormat.Processes(null));
    }

    [Fact]
    public void Gpu_names_are_joined_without_duplicates()
    {
        Assert.Equal("NVIDIA GeForce RTX 4060 Ti + Intel(R) UHD Graphics 770", SystemInfoFormat.GpuNames(new[]
        {
            "NVIDIA GeForce RTX 4060 Ti", "Intel(R) UHD Graphics 770", "NVIDIA GeForce RTX 4060 Ti", " ",
        }));
        Assert.Equal("—", SystemInfoFormat.GpuNames(Array.Empty<string>()));
    }
}
