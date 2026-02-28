using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Auth.Common;
using Katalogcu.Domain.Enums;
using MediatR;

namespace Katalogcu.Application.Features.Auth.Commands.SelectPlan;

public sealed class SelectPlanCommandHandler : IRequestHandler<SelectPlanCommand, OperationResult<AuthUserDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthRepository _authRepository;

    public SelectPlanCommandHandler(ICurrentUserService currentUser, IAuthRepository authRepository)
    {
        _currentUser = currentUser;
        _authRepository = authRepository;
    }

    public async Task<OperationResult<AuthUserDto>> Handle(SelectPlanCommand request, CancellationToken cancellationToken)
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

        var plan = (SubscriptionPlan)request.Plan;
        user.SubscriptionPlan = plan;
        user.PlanActivatedAt = DateTime.UtcNow;
        user.PlanExpiresAt = null;
        user.MaxCatalogCount = plan switch
        {
            SubscriptionPlan.CatalogWithAI => 10,
            SubscriptionPlan.CatalogWithAIAndEcommerce => int.MaxValue,
            _ => 5
        };
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
