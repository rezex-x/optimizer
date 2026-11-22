using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class GpuVendorsTests
{
    [Theory]
    [InlineData("NVIDIA GeForce RTX 4060 Ti", true, false, false)]
    [InlineData("AMD Radeon RX 7800 XT", false, true, false)]
    [InlineData("Radeon(TM) Graphics", false, true, false)]
    [InlineData("Intel(R) UHD Graphics 770", false, false, true)]
    public void Flags_match_vendor(string name, bool nv, bool amd, bool intel)
    {
        var g = GpuVendors.From(new[] { name });
        Assert.Equal(nv, g.HasNvidia);
        Assert.Equal(amd, g.HasAmd);
        Assert.Equal(intel, g.HasIntel);
    }

    [Fact]
    public void Empty_and_blank_names_yield_no_vendors()
    {
        var g = GpuVendors.From(new[] { "", "  ", null! });
        Assert.False(g.HasNvidia || g.HasAmd || g.HasIntel);
    }
}
