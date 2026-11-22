using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class GpuEngineParserTests
{
    [Fact]
    public void The_pid_is_read_from_the_instance_name()
    {
        Assert.Equal(18420, GpuEngineParser.Pid("pid_18420_luid_0x00000000_0x0000D1A4_phys_0_eng_0_engtype_3D"));
        Assert.Null(GpuEngineParser.Pid("luid_0x00000000_0x0000D1A4_phys_0_eng_0"));
        Assert.Null(GpuEngineParser.Pid("pid__luid"));
    }

    [Fact]
    public void Per_process_gpu_is_the_busiest_engine_clamped_to_100()
    {
        var perProcess = GpuEngineParser.PerProcess(new[]
        {
            ("pid_100_luid_0x0_0x1_phys_0_eng_0_engtype_3D", 40.0),
            ("pid_100_luid_0x0_0x1_phys_0_eng_3_engtype_VideoDecode", 12.0),
            ("pid_200_luid_0x0_0x1_phys_0_eng_0_engtype_3D", 130.0),
            ("_Total", 50.0),
        });

        Assert.Equal(40.0, perProcess[100]);
        Assert.Equal(100.0, perProcess[200]);
        Assert.Equal(2, perProcess.Count);
    }
}
