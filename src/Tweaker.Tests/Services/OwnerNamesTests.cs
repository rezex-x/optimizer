using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class OwnerNamesTests
{
    [Fact]
    public void Owners_read_user_or_the_short_account_name()
    {
        Assert.Equal("User", OwnerNames.Display(@"DESKTOP-1\Player", isCurrentUser: true));
        Assert.Equal("SYSTEM", OwnerNames.Display(@"NT AUTHORITY\SYSTEM", false));
        Assert.Equal("NETWORK SERVICE", OwnerNames.Display(@"NT AUTHORITY\NETWORK SERVICE", false));
        Assert.Equal("DWM-1", OwnerNames.Display(@"Window Manager\DWM-1", false));
        Assert.Equal("—", OwnerNames.Display(null, false));
    }
}
