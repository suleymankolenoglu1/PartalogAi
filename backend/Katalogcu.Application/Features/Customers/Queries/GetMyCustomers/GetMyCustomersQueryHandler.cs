using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Queries.GetMyCustomers;

public sealed class GetMyCustomersQueryHandler : IRequestHandler<GetMyCustomersQuery, OperationResult<IReadOnlyList<CustomerListItemDto>>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICustomerRepository _customerRepository;

    public GetMyCustomersQueryHandler(ICurrentUserService currentUser, ICustomerRepository customerRepository)
    {
        _currentUser = currentUser;
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<IReadOnlyList<CustomerListItemDto>>> Handle(GetMyCustomersQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<IReadOnlyList<CustomerListItemDto>>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var customers = await _customerRepository.GetCustomersByUserAsync(_currentUser.UserId, cancellationToken);
        var result = customers.Select(c => new CustomerListItemDto
        {
            Id = c.Id,
            Name = c.FullName,
            Company = c.CompanyName,
            Email = c.Email,
            Phone = c.Phone,
            OrderCount = c.OrderCount,
            TotalSpent = c.TotalSpent,
            LastVisitDate = c.LastVisitDate,
            LastOrderDate = c.LastOrderDate,
            LastLoginDate = c.LastLoginDate,
            LastActivityDate = c.LastLoginDate ?? c.LastOrderDate ?? c.LastVisitDate,
            HasPassword = !string.IsNullOrWhiteSpace(c.PasswordHash) && !string.IsNullOrWhiteSpace(c.PasswordSalt),
            Status = c.IsActive ? "active" : "inactive",
            Note = c.Note,
            CreatedDate = c.CreatedDate
        }).ToList();

        return OperationResult<IReadOnlyList<CustomerListItemDto>>.Success(result);
    }
}
