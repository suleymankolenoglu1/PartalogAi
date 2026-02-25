using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Common.Security;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Users.Commands.CreateUser;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, OperationResult<AppUser>>
{
    private readonly IUserRepository _userRepository;

    public CreateUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<OperationResult<AppUser>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        UserPasswordHasher.CreatePasswordHash(request.Password, out var passwordHash, out var passwordSalt);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName?.Trim() ?? string.Empty,
            Email = request.Email.Trim(),
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            Role = string.IsNullOrWhiteSpace(request.Role) ? "Customer" : request.Role,
            CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? null : request.CompanyName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            CreatedDate = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<AppUser>.Success(user);
    }
}
