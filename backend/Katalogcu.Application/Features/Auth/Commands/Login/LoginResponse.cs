using Katalogcu.Application.Features.Auth.Common;

namespace Katalogcu.Application.Features.Auth.Commands.Login;

public sealed class LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public AuthUserDto User { get; init; } = new();
}
