using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.UpdatePublicCustomerMachine;

public sealed class UpdatePublicCustomerMachineCommandHandler
    : IRequestHandler<UpdatePublicCustomerMachineCommand, OperationResult<PublicCustomerMachineDto>>
{
    private readonly ICustomerRepository _customerRepository;

    public UpdatePublicCustomerMachineCommandHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<PublicCustomerMachineDto>> Handle(
        UpdatePublicCustomerMachineCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByPublicSessionAsync(
            request.OwnerUserId,
            request.SessionToken,
            DateTime.UtcNow,
            cancellationToken);

        if (customer == null)
        {
            return OperationResult<PublicCustomerMachineDto>.Failure("unauthorized", "Oturum geçersiz.");
        }

        var machine = await _customerRepository.GetMachineByIdAsync(customer.Id, request.MachineId, cancellationToken);
        if (machine == null)
        {
            return OperationResult<PublicCustomerMachineDto>.Failure("not_found", "Makine bulunamadı.");
        }

        if (request.IsActive && !machine.IsActive)
        {
            var machines = await _customerRepository.GetMachinesByCustomerIdAsync(customer.Id, cancellationToken);
            foreach (var current in machines.Where(m => m.IsActive && m.Id != machine.Id))
            {
                current.IsActive = false;
                current.UpdatedDate = DateTime.UtcNow;
            }
        }

        machine.Brand = request.Brand.Trim();
        machine.Model = request.Model.Trim();
        machine.Variant = NormalizeOptional(request.Variant);
        machine.MachineGroup = NormalizeOptional(request.MachineGroup);
        machine.SerialNumber = NormalizeOptional(request.SerialNumber);
        machine.DisplayName = NormalizeOptional(request.DisplayName)
            ?? string.Join(' ', new[] { machine.Brand, machine.Model, machine.Variant }.Where(x => !string.IsNullOrWhiteSpace(x)));
        machine.IsActive = request.IsActive;
        machine.UpdatedDate = DateTime.UtcNow;

        await _customerRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<PublicCustomerMachineDto>.Success(CustomerMachineMapper.ToDto(machine));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
