using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.Auth.Commands.SelectPlan;
using Katalogcu.Domain.Entities;
using Katalogcu.Domain.Enums;
using Xunit;

namespace Katalogcu.API.Tests.Features.Auth;

public sealed class SelectPlanCommandHandlerTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Handle_RejectsSelfServicePaidPlanUpgrade(int requestedPlan)
    {
        var user = CreateUser();
        var repository = new FakeAuthRepository(user);
        var handler = new SelectPlanCommandHandler(new FakeCurrentUser(user.Id), repository);

        var result = await handler.Handle(new SelectPlanCommand(requestedPlan), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("forbidden", result.ErrorCode);
        Assert.Equal(SubscriptionPlan.CatalogOnly, user.SubscriptionPlan);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Handle_AllowsDowngradeToCatalogOnly()
    {
        var user = CreateUser();
        user.SubscriptionPlan = SubscriptionPlan.CatalogWithAIAndEcommerce;
        user.MaxCatalogCount = int.MaxValue;
        var repository = new FakeAuthRepository(user);
        var handler = new SelectPlanCommandHandler(new FakeCurrentUser(user.Id), repository);

        var result = await handler.Handle(
            new SelectPlanCommand((int)SubscriptionPlan.CatalogOnly),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionPlan.CatalogOnly, user.SubscriptionPlan);
        Assert.Equal(5, user.MaxCatalogCount);
        Assert.Equal(1, repository.SaveCount);
    }

    private static AppUser CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Test",
        LastName = "Owner",
        Email = "owner@example.test",
        Role = "Owner"
    };

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public string ActorName => "test";
    }

    private sealed class FakeAuthRepository(AppUser user) : IAuthRepository
    {
        public int SaveCount { get; private set; }

        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken)
            => Task.FromResult<AppUser?>(user.Email == email ? user : null);

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
            => Task.FromResult(user.Email == email);

        public Task AddUserAsync(AppUser newUser, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<AppUser?>(user.Id == userId ? user : null);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
