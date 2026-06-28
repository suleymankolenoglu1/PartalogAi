using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Chat.Queries.ResolveChatCatalogAccess;

public sealed class ResolveChatCatalogAccessQueryHandler : IRequestHandler<ResolveChatCatalogAccessQuery, OperationResult<ResolveChatCatalogAccessResponse>>
{
    private readonly IPublicAccessTokenService _publicAccessTokenService;
    private readonly IChatQueryService _chatQueryService;

    public ResolveChatCatalogAccessQueryHandler(
        IPublicAccessTokenService publicAccessTokenService,
        IChatQueryService chatQueryService)
    {
        _publicAccessTokenService = publicAccessTokenService;
        _chatQueryService = chatQueryService;
    }

    public async Task<OperationResult<ResolveChatCatalogAccessResponse>> Handle(
        ResolveChatCatalogAccessQuery request,
        CancellationToken cancellationToken)
    {
        Guid tokenUserId = request.AuthenticatedUserId;
        Guid? publicUserId = null;
        IReadOnlyCollection<Guid>? publicAllowedCatalogIds = null;

        if (tokenUserId == Guid.Empty)
        {
            if (string.IsNullOrWhiteSpace(request.PublicToken))
            {
                return OperationResult<ResolveChatCatalogAccessResponse>.Failure("validation", "Katalog bilgisi bulunamadı.");
            }

            var payload = _publicAccessTokenService.Validate(request.PublicToken);
            if (payload == null || payload.UserId == Guid.Empty)
            {
                return OperationResult<ResolveChatCatalogAccessResponse>.Failure("validation", "Katalog bilgisi bulunamadı.");
            }

            publicUserId = payload.UserId;
            publicAllowedCatalogIds = payload.CatalogIds;
        }

        var catalogIds = await _chatQueryService.ResolveAccessibleCatalogIdsAsync(
            tokenUserId,
            publicUserId,
            publicAllowedCatalogIds,
            request.RequestedCatalogIds,
            cancellationToken);

        return OperationResult<ResolveChatCatalogAccessResponse>.Success(new ResolveChatCatalogAccessResponse
        {
            CatalogIds = catalogIds
        });
    }
}
