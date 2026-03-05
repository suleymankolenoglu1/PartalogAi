using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Common.Security;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Auth.Commands.Register;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, OperationResult<RegisterResponse>>
{
    private readonly IAuthRepository _authRepository;

    public RegisterCommandHandler(IAuthRepository authRepository)
    {
        _authRepository = authRepository;
    }

    public async Task<OperationResult<RegisterResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _authRepository.EmailExistsAsync(email, cancellationToken))
        {
            return OperationResult<RegisterResponse>.Failure("duplicate", "Bu e-posta adresi zaten kayıtlı!");
        }

        var names = request.FullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = names.Length > 0 ? names[0] : string.Empty;
        var lastName = names.Length > 1 ? names[1] : string.Empty;
        UserPasswordHasher.CreatePasswordHash(request.Password, out var passwordHash, out var passwordSalt);

        var newUser = new AppUser
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            Role = "Owner",
            CreatedDate = DateTime.UtcNow
        };

        await _authRepository.AddUserAsync(newUser, cancellationToken);
        await _authRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<RegisterResponse>.Success(new RegisterResponse());
    }
}
