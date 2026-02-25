using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerMe;

public sealed class GetPublicCustomerMeQueryHandler : IRequestHandler<GetPublicCustomerMeQuery, OperationResult<PublicCustomerDto>>
{
    private readonly ICustomerRepository _customerRepository;

    public GetPublicCustomerMeQueryHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<PublicCustomerDto>> Handle(GetPublicCustomerMeQuery request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByPublicSessionAsync(
            request.OwnerUserId,
            request.SessionToken,
            DateTime.UtcNow,
            cancellationToken);

        if (customer == null)
        {
            return OperationResult<PublicCustomerDto>.Failure("unauthorized", "Oturum geçersiz.");
        }

        return OperationResult<PublicCustomerDto>.Success(new PublicCustomerDto
        {
            Id = customer.Id,
            Name = customer.FullName,
            Phone = customer.Phone,
            Email = customer.Email,
            Company = customer.CompanyName,
            LastLoginDate = customer.LastLoginDate
        });
    }
}
