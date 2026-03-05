using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Common.Security;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Users.Commands.CreateUser;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, OperationResult<AppUser>>
{
    private readonly IUserRepository _userRepository;
    private readonly IAuthRepository _authRepository;

    public CreateUserCommandHandler(IUserRepository userRepository, IAuthRepository authRepository)
    {
        _userRepository = userRepository;
        _authRepository = authRepository;
    }

    public async Task<OperationResult<AppUser>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _authRepository.EmailExistsAsync(email, cancellationToken))
        {
            return OperationResult<AppUser>.Failure("duplicate", "Bu e-posta adresi zaten kayıtlı.");
        }

        UserPasswordHasher.CreatePasswordHash(request.Password, out var passwordHash, out var passwordSalt);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName?.Trim() ?? string.Empty,
            Email = email,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            Role = NormalizeRole(request.Role),
            CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? null : request.CompanyName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            CreatedDate = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<AppUser>.Success(user);
    }

    private static string NormalizeRole(string? role)
    {
        if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            return "Owner";
        }

        return "Customer";
    }
}
