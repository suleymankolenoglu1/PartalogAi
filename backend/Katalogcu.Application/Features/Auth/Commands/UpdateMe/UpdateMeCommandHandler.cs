using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Auth.Common;
using MediatR;

namespace Katalogcu.Application.Features.Auth.Commands.UpdateMe;

public sealed class UpdateMeCommandHandler : IRequestHandler<UpdateMeCommand, OperationResult<AuthUserDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthRepository _authRepository;

    public UpdateMeCommandHandler(ICurrentUserService currentUser, IAuthRepository authRepository)
    {
        _currentUser = currentUser;
        _authRepository = authRepository;
    }

    public async Task<OperationResult<AuthUserDto>> Handle(UpdateMeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<AuthUserDto>.Failure("unauthorized", "Unauthorized");
        }

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            return OperationResult<AuthUserDto>.Failure("validation", "Ad ve soyad zorunludur.");
        }

        var user = await _authRepository.GetByIdAsync(_currentUser.UserId, cancellationToken);
        if (user == null)
        {
            return OperationResult<AuthUserDto>.Failure("not_found", "Kullanıcı bulunamadı.");
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? null : request.CompanyName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.UpdatedDate = DateTime.UtcNow;

        await _authRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<AuthUserDto>.Success(new AuthUserDto
        {
            Id = user.Id,
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            CompanyName = user.CompanyName,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role
        });
    }
}
