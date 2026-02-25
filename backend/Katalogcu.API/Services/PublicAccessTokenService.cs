using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;

namespace Katalogcu.API.Services;

public sealed class PublicAccessTokenService : IPublicAccessTokenService
{
    private readonly IPublicLinkService _publicLinkService;

    public PublicAccessTokenService(IPublicLinkService publicLinkService)
    {
        _publicLinkService = publicLinkService;
    }

    public PublicAccessPayloadDto? Validate(string token)
    {
        var payload = _publicLinkService.Validate(token);
        if (payload == null)
        {
            return null;
        }

        return new PublicAccessPayloadDto
        {
            UserId = payload.UserId,
            CatalogIds = payload.CatalogIds
        };
    }
}
