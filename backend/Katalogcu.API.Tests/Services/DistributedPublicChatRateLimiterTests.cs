using Katalogcu.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class DistributedPublicChatRateLimiterTests
{
    [Fact]
    public async Task TryAcquireAsync_AllowsWhenRedisPublicChatLimitIsDisabled()
    {
        var limiter = new RedisDistributedPublicChatRateLimiter(
            Options.Create(new DistributedRateLimitOptions
            {
                RedisPublicChatEnabled = false
            }),
            NullLogger<RedisDistributedPublicChatRateLimiter>.Instance);

        var result = await limiter.TryAcquireAsync(new DefaultHttpContext(), CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.Equal("disabled", result.Reason);
    }
}
