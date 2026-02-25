using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Chat.Queries.ResolveChatUser;

public sealed class ResolveChatUserQueryHandler : IRequestHandler<ResolveChatUserQuery, OperationResult<ResolveChatUserResponse>>
{
    private readonly IPublicAccessTokenService _publicAccessTokenService;

    public ResolveChatUserQueryHandler(IPublicAccessTokenService publicAccessTokenService)
    {
        _publicAccessTokenService = publicAccessTokenService;
    }

    public Task<OperationResult<ResolveChatUserResponse>> Handle(ResolveChatUserQuery request, CancellationToken cancellationToken)
    {
        if (request.AuthenticatedUserId != Guid.Empty)
        {
            return Task.FromResult(OperationResult<ResolveChatUserResponse>.Success(new ResolveChatUserResponse
            {
                UserId = request.AuthenticatedUserId,
                IsPublic = false
            }));
        }

        if (string.IsNullOrWhiteSpace(request.PublicToken))
        {
            return Task.FromResult(OperationResult<ResolveChatUserResponse>.Failure(
                "validation",
                "Geçerli kullanıcı veya public token gerekli."));
        }

        var payload = _publicAccessTokenService.Validate(request.PublicToken);
        if (payload == null || payload.UserId == Guid.Empty)
        {
            return Task.FromResult(OperationResult<ResolveChatUserResponse>.Failure(
                "validation",
                "Geçerli kullanıcı veya public token gerekli."));
        }

        return Task.FromResult(OperationResult<ResolveChatUserResponse>.Success(new ResolveChatUserResponse
        {
            UserId = payload.UserId,
            IsPublic = true
        }));
    }
}
