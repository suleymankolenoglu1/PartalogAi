using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Features.Customers.Common;

public static class CustomerMachineMapper
{
    public static PublicCustomerMachineDto ToDto(CustomerMachine machine)
    {
        return new PublicCustomerMachineDto
        {
            Id = machine.Id,
            Brand = machine.Brand,
            Model = machine.Model,
            Variant = machine.Variant,
            MachineGroup = machine.MachineGroup,
            SerialNumber = machine.SerialNumber,
            DisplayName = machine.DisplayName,
            IsActive = machine.IsActive,
            CreatedDate = machine.CreatedDate
        };
    }
}
