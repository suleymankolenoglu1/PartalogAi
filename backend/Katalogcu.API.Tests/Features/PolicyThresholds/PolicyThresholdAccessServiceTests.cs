using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using Katalogcu.Domain.Entities;
using Xunit;

namespace Katalogcu.API.Tests.Features.PolicyThresholds;

public sealed class PolicyThresholdAccessServiceTests
{
    [Fact]
    public async Task ValidateScopeAccessAsync_AllowsPlatformAdminForGlobalScope()
    {
        var service = new PolicyThresholdAccessService(new FakePolicyThresholdRepository());

        var result = await service.ValidateScopeAccessAsync(
            PolicyThreshold.GlobalScope,
            "default",
            Actor(isPlatformAdmin: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Theory]
    [InlineData("Global")]
    [InlineData("Brand")]
    public async Task ValidateScopeAccessAsync_RejectsNonPlatformAdminForGlobalAndBrandScopes(string scopeType)
    {
        var service = new PolicyThresholdAccessService(new FakePolicyThresholdRepository());

        var result = await service.ValidateScopeAccessAsync(
            scopeType,
            "scope-key",
            Actor(isPlatformAdmin: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("forbidden", result.ErrorCode);
        Assert.Equal("Bu scope için yetki yok.", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateScopeAccessAsync_RejectsInvalidCatalogScopeKey()
    {
        var service = new PolicyThresholdAccessService(new FakePolicyThresholdRepository());

        var result = await service.ValidateScopeAccessAsync(
            PolicyThreshold.CatalogScope,
            "not-a-guid",
            Actor(isPlatformAdmin: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("validation", result.ErrorCode);
        Assert.Equal("Catalog scope için ScopeKey geçerli bir catalog id olmalıdır.", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateScopeAccessAsync_AllowsPlatformAdminForCatalogWithoutOwnershipCheck()
    {
        var repository = new FakePolicyThresholdRepository { UserOwnsCatalogResult = false };
        var service = new PolicyThresholdAccessService(repository);

        var result = await service.ValidateScopeAccessAsync(
            PolicyThreshold.CatalogScope,
            Guid.NewGuid().ToString(),
            Actor(isPlatformAdmin: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(0, repository.UserOwnsCatalogCallCount);
    }

    [Fact]
    public async Task ValidateScopeAccessAsync_AllowsCatalogOwner()
    {
        var repository = new FakePolicyThresholdRepository { UserOwnsCatalogResult = true };
        var service = new PolicyThresholdAccessService(repository);

        var result = await service.ValidateScopeAccessAsync(
            PolicyThreshold.CatalogScope,
            Guid.NewGuid().ToString(),
            Actor(isPlatformAdmin: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(1, repository.UserOwnsCatalogCallCount);
    }

    [Fact]
    public async Task ValidateScopeAccessAsync_RejectsNonOwnerForCatalog()
    {
        var repository = new FakePolicyThresholdRepository { UserOwnsCatalogResult = false };
        var service = new PolicyThresholdAccessService(repository);

        var result = await service.ValidateScopeAccessAsync(
            PolicyThreshold.CatalogScope,
            Guid.NewGuid().ToString(),
            Actor(isPlatformAdmin: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("forbidden", result.ErrorCode);
        Assert.Equal("Bu catalog için yetki yok.", result.ErrorMessage);
        Assert.Equal(1, repository.UserOwnsCatalogCallCount);
    }

    private static PolicyThresholdActor Actor(bool isPlatformAdmin)
        => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            isPlatformAdmin,
            "actor@example.com",
            isPlatformAdmin ? "platformadmin" : "admin",
            "127.0.0.1",
            "tests");

    private sealed class FakePolicyThresholdRepository : IPolicyThresholdRepository
    {
        public bool UserOwnsCatalogResult { get; init; }
        public int UserOwnsCatalogCallCount { get; private set; }

        public Task<IReadOnlyList<PolicyThreshold>> GetPolicyThresholdsAsync(
            bool includeInactive,
            string? scopeType,
            Guid actorUserId,
            bool isPlatformAdmin,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PolicyThreshold?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PolicyThreshold?> GetActiveAsync(string scopeType, string scopeKey, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> UserOwnsCatalogAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken)
        {
            UserOwnsCatalogCallCount++;
            return Task.FromResult(UserOwnsCatalogResult);
        }

        public Task<IReadOnlyList<string>> GetOwnedCatalogScopeKeysAsync(Guid userId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<PolicyThresholdOperationLog>> GetRecentPolicyOperationLogsAsync(
            int take,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public void AddPolicyThreshold(PolicyThreshold threshold)
            => throw new NotSupportedException();

        public void AddAuditLog(PlatformAuditLog auditLog)
            => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
