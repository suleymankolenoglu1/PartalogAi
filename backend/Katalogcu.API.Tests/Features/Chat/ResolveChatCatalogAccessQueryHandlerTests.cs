using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Chat.Common;
using Katalogcu.Application.Features.Chat.Queries.ResolveChatCatalogAccess;
using Katalogcu.Domain.Entities;
using Xunit;

namespace Katalogcu.API.Tests.Features.Chat;

public sealed class ResolveChatCatalogAccessQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsSuccessWithEmptyCatalogs_WhenPublicTokenIsValid()
    {
        var publicUserId = Guid.NewGuid();
        var handler = new ResolveChatCatalogAccessQueryHandler(
            new StubPublicAccessTokenService(new PublicAccessPayloadDto { UserId = publicUserId }),
            new StubChatQueryService([]));

        var result = await handler.Handle(
            new ResolveChatCatalogAccessQuery(Guid.Empty, "valid-token", []),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.CatalogIds);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenAnonymousRequestHasNoPublicToken()
    {
        var handler = new ResolveChatCatalogAccessQueryHandler(
            new StubPublicAccessTokenService(null),
            new StubChatQueryService([]));

        var result = await handler.Handle(
            new ResolveChatCatalogAccessQuery(Guid.Empty, null, []),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("validation", result.ErrorCode);
    }

    private sealed class StubPublicAccessTokenService(PublicAccessPayloadDto? payload) : IPublicAccessTokenService
    {
        public PublicAccessPayloadDto? Validate(string token) => payload;
    }

    private sealed class StubChatQueryService(IReadOnlyList<Guid> catalogIds) : IChatQueryService
    {
        public Task<IReadOnlyList<Guid>> ResolveAccessibleCatalogIdsAsync(
            Guid tokenUserId,
            Guid? publicUserId,
            IReadOnlyCollection<Guid>? publicAllowedCatalogIds,
            IReadOnlyCollection<Guid> requestedCatalogIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(catalogIds);
        }

        public Task<IReadOnlyList<EnrichedPartDto>> EnrichPythonSourcesAsync(
            IReadOnlyCollection<ChatSourceInput> sources,
            IReadOnlyCollection<Guid> catalogIds,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<CatalogItem>> SearchByCodeAsync(
            string? term,
            IReadOnlyCollection<Guid> catalogIds,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<EnrichedPartDto>> EnrichResultsAsync(
            IReadOnlyCollection<CatalogItem> items,
            IReadOnlyCollection<Guid> catalogIds,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
