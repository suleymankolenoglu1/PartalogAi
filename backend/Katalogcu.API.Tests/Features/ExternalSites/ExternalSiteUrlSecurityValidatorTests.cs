using System.Net;
using Katalogcu.Application.Features.ExternalSites.Commands;
using Xunit;

namespace Katalogcu.API.Tests.Features.ExternalSites;

public sealed class ExternalSiteUrlSecurityValidatorTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("192.168.1.10")]
    [InlineData("198.51.100.10")]
    [InlineData("203.0.113.10")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("2001:db8::1")]
    public void IsPrivateOrLocalAddress_BlocksNonPublicRanges(string value)
    {
        Assert.True(ExternalSiteUrlSecurityValidator.IsPrivateOrLocalAddress(IPAddress.Parse(value)));
    }

    [Theory]
    [InlineData("http://localhost/admin")]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("file:///etc/passwd")]
    public async Task IsSafeExternalUrlAsync_RejectsUnsafeDestinations(string value)
    {
        Assert.False(await ExternalSiteUrlSecurityValidator.IsSafeExternalUrlAsync(value, CancellationToken.None));
    }
}
