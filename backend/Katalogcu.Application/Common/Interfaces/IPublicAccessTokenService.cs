using Katalogcu.Application.Common.Models;

namespace Katalogcu.Application.Common.Interfaces;

public interface IPublicAccessTokenService
{
    PublicAccessPayloadDto? Validate(string token);
}
