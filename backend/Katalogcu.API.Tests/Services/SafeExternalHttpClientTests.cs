using Katalogcu.Infrastructure.Services;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class SafeExternalHttpClientTests
{
    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("file:///etc/passwd")]
    public async Task SendAsync_RejectsUnsafeDestinationBeforeConnecting(string url)
    {
        using var client = new SafeExternalHttpClient();

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(
            HttpMethod.Get,
            url,
            HttpCompletionOption.ResponseHeadersRead,
            CancellationToken.None));
    }
}
