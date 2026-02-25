using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Chat.Queries.ResolveChatCatalogAccess;

public sealed record ResolveChatCatalogAccessQuery(
    Guid AuthenticatedUserId,
    string? PublicToken,
    IReadOnlyCollection<Guid> RequestedCatalogIds)
    : IRequest<OperationResult<ResolveChatCatalogAccessResponse>>;

public sealed class ResolveChatCatalogAccessResponse
{
    public IReadOnlyList<Guid> CatalogIds { get; init; } = [];
}
