using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string CreateToken(AppUser user);
}
