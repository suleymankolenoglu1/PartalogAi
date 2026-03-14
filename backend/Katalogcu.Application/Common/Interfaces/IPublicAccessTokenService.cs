using Katalogcu.Application.Common.Models;

namespace Katalogcu.Application.Common.Interfaces;

public interface IPublicAccessTokenService
{
    PublicAccessPayloadDto? Validate(string token);
    string CreateEmbedSessionToken(Guid userId, IReadOnlyList<Guid> catalogIds, string embedKey, DateTime expiresAtUtc);
}
