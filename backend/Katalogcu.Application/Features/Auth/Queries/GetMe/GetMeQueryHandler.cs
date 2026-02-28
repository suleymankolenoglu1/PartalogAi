using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Auth.Common;
using MediatR;

namespace Katalogcu.Application.Features.Auth.Queries.GetMe;

public sealed class GetMeQueryHandler : IRequestHandler<GetMeQuery, OperationResult<AuthUserDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthRepository _authRepository;

    public GetMeQueryHandler(ICurrentUserService currentUser, IAuthRepository authRepository)
    {
        _currentUser = currentUser;
        _authRepository = authRepository;
    }

    public async Task<OperationResult<AuthUserDto>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<AuthUserDto>.Failure("unauthorized", "Unauthorized");
        }

        var user = await _authRepository.GetByIdAsync(_currentUser.UserId, cancellationToken);
        if (user == null)
        {
            return OperationResult<AuthUserDto>.Failure("not_found", "Kullanıcı bulunamadı.");
        }

        return OperationResult<AuthUserDto>.Success(new AuthUserDto
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
        });
    }
}
