using Katalogcu.API.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class AiCapacityGuardTests
{
    [Fact]
    public async Task TryAcquireAsync_EnforcesPerUserConcurrencyLimit()
    {
        var guard = CreateGuard(globalLimit: 4, perUserLimit: 1);
        var userId = Guid.NewGuid();

        await using var first = await guard.TryAcquireAsync(userId, null, CancellationToken.None);
        var second = await guard.TryAcquireAsync(userId, null, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Equal(1, guard.GetSnapshot().GlobalActiveChats);
    }

    [Fact]
    public async Task TryAcquireAsync_EnforcesGlobalConcurrencyLimitAcrossUsers()
    {
        var guard = CreateGuard(globalLimit: 1, perUserLimit: 3);

        await using var first = await guard.TryAcquireAsync(Guid.NewGuid(), null, CancellationToken.None);
        var second = await guard.TryAcquireAsync(Guid.NewGuid(), null, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Equal(1, guard.GetSnapshot().GlobalActiveChats);
    }

    [Fact]
    public async Task LeaseDispose_ReleasesCapacity()
    {
        var guard = CreateGuard(globalLimit: 1, perUserLimit: 1);
        var userId = Guid.NewGuid();

        var first = await guard.TryAcquireAsync(userId, null, CancellationToken.None);
        Assert.NotNull(first);
        await first!.DisposeAsync();

        await using var second = await guard.TryAcquireAsync(userId, null, CancellationToken.None);
        Assert.NotNull(second);
        Assert.Equal(1, guard.GetSnapshot().GlobalActiveChats);
    }

    [Fact]
    public void GetSnapshot_DefaultsToInMemoryMode()
    {
        var guard = CreateGuard(globalLimit: 4, perUserLimit: 2);

        var snapshot = guard.GetSnapshot();

        Assert.Equal("in-memory", snapshot.Mode);
        Assert.False(snapshot.Distributed);
    }

    [Fact]
    public void GetSnapshot_UsesRedisModeWhenProviderIsRedis()
    {
        var guard = new AiCapacityGuard(Options.Create(new AiCapacityOptions
        {
            Provider = "Redis",
            RedisConnectionString = "localhost:6379,abortConnect=false",
            GlobalConcurrentChats = 4,
            PerUserConcurrentChats = 2,
            AcquireTimeoutMs = 0
        }));

        var snapshot = guard.GetSnapshot();

        Assert.Equal("redis-distributed", snapshot.Mode);
        Assert.True(snapshot.Distributed);
    }

    [Fact]
    public async Task CheckHealthAsync_InMemoryProviderIsReady()
    {
        var guard = CreateGuard(globalLimit: 4, perUserLimit: 2);

        var health = await guard.CheckHealthAsync(CancellationToken.None);

        Assert.True(health.Ready);
        Assert.Equal("in-memory", health.Mode);
        Assert.Null(health.Error);
    }

    private static AiCapacityGuard CreateGuard(int globalLimit, int perUserLimit)
    {
        return new AiCapacityGuard(Options.Create(new AiCapacityOptions
        {
            GlobalConcurrentChats = globalLimit,
            PerUserConcurrentChats = perUserLimit,
            AcquireTimeoutMs = 0
        }));
    }
}
