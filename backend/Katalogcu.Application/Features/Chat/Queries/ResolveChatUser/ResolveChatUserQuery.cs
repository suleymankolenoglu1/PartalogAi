using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Chat.Queries.ResolveChatUser;

public sealed record ResolveChatUserQuery(Guid AuthenticatedUserId, string? PublicToken)
    : IRequest<OperationResult<ResolveChatUserResponse>>;

public sealed class ResolveChatUserResponse
{
    public Guid UserId { get; init; }
    public bool IsPublic { get; init; }
}
