using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Common.Security;
using Katalogcu.Application.Features.Auth.Common;
using MediatR;

namespace Katalogcu.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, OperationResult<LoginResponse>>
{
    private readonly IAuthRepository _authRepository;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginCommandHandler(IAuthRepository authRepository, IJwtTokenService jwtTokenService)
    {
        _authRepository = authRepository;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<OperationResult<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _authRepository.GetByEmailAsync(email, cancellationToken);
        if (user == null)
        {
            return OperationResult<LoginResponse>.Failure("unauthorized", "Email veya şifre hatalı!");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordSalt))
        {
            return OperationResult<LoginResponse>.Failure(
                "password_upgrade_required",
                "Hesabınız için güvenli şifre güncellemesi gerekiyor. Lütfen şifre sıfırlama adımını kullanın.");
        }

        var isPasswordValid = UserPasswordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt!);

        if (!isPasswordValid)
        {
            return OperationResult<LoginResponse>.Failure("unauthorized", "Email veya şifre hatalı!");
        }

        var token = _jwtTokenService.CreateToken(user);
        return OperationResult<LoginResponse>.Success(new LoginResponse
        {
            Token = token,
            User = new AuthUserDto
            {
                Id = user.Id,
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                CompanyName = user.CompanyName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                SubscriptionPlan = (int)user.SubscriptionPlan,
                PlanActivatedAt = user.PlanActivatedAt,
                PlanExpiresAt = user.PlanExpiresAt,
                PlanSelected = user.PlanActivatedAt.HasValue,
                MaxCatalogCount = user.MaxCatalogCount,
                MaxPagePerCatalog = user.MaxPagePerCatalog
            }
        });
    }
}
