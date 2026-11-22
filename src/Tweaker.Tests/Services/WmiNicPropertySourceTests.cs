using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class WmiNicPropertySourceTests
{
    [Theory]
    [InlineData("Ethernet [2]")]
    [InlineData("Ethernet*")]
    [InlineData("Wi-Fi?")]
    public void Adapter_names_with_wildcard_characters_are_refused(string name)
    {
        var runner = new FakeProcessRunner();

        string? error = new WmiNicPropertySource(runner).Set(name, "*EEE", "0");

        Assert.Equal("unsupported characters in the adapter name", error);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void A_plain_adapter_name_is_passed_to_powershell()
    {
        var runner = new FakeProcessRunner();

        string? error = new WmiNicPropertySource(runner).Set("Ethernet 2", "*EEE", "0");

        Assert.Null(error);
        Assert.Contains("-Name 'Ethernet 2'", Assert.Single(runner.Calls).arguments);
    }
}
