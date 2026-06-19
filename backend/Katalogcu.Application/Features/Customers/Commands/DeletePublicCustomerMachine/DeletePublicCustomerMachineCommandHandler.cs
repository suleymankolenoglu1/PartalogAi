using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.DeletePublicCustomerMachine;

public sealed class DeletePublicCustomerMachineCommandHandler
    : IRequestHandler<DeletePublicCustomerMachineCommand, OperationResult<bool>>
{
    private readonly ICustomerRepository _customerRepository;

    public DeletePublicCustomerMachineCommandHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<bool>> Handle(
        DeletePublicCustomerMachineCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByPublicSessionAsync(
            request.OwnerUserId,
            request.SessionToken,
            DateTime.UtcNow,
            cancellationToken);

        if (customer == null)
        {
            return OperationResult<bool>.Failure("unauthorized", "Oturum geçersiz.");
        }

        var machine = await _customerRepository.GetMachineByIdAsync(customer.Id, request.MachineId, cancellationToken);
        if (machine == null)
        {
            return OperationResult<bool>.Failure("not_found", "Makine bulunamadı.");
        }

        var wasActive = machine.IsActive;
        _customerRepository.RemoveMachine(machine);
        await _customerRepository.SaveChangesAsync(cancellationToken);

        if (wasActive)
        {
            var machines = await _customerRepository.GetMachinesByCustomerIdAsync(customer.Id, cancellationToken);
            var replacement = machines.FirstOrDefault();
            if (replacement != null)
            {
                replacement.IsActive = true;
                replacement.UpdatedDate = DateTime.UtcNow;
                await _customerRepository.SaveChangesAsync(cancellationToken);
            }
        }

        return OperationResult<bool>.Success(true);
    }
}
