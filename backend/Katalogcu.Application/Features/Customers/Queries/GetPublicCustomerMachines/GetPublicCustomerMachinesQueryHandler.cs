using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerMachines;

public sealed class GetPublicCustomerMachinesQueryHandler
    : IRequestHandler<GetPublicCustomerMachinesQuery, OperationResult<IReadOnlyList<PublicCustomerMachineDto>>>
{
    private readonly ICustomerRepository _customerRepository;

    public GetPublicCustomerMachinesQueryHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<IReadOnlyList<PublicCustomerMachineDto>>> Handle(
        GetPublicCustomerMachinesQuery request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByPublicSessionAsync(
            request.OwnerUserId,
            request.SessionToken,
            DateTime.UtcNow,
            cancellationToken);

        if (customer == null)
        {
            return OperationResult<IReadOnlyList<PublicCustomerMachineDto>>.Failure("unauthorized", "Oturum geçersiz.");
        }

        var machines = await _customerRepository.GetMachinesByCustomerIdAsync(customer.Id, cancellationToken);
        return OperationResult<IReadOnlyList<PublicCustomerMachineDto>>.Success(
            machines.Select(CustomerMachineMapper.ToDto).ToList());
    }
}
