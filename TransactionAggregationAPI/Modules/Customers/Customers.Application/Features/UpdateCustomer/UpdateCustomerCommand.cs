using SharedKernel.Abstractions;

namespace Modules.Customers.Application.Features.UpdateCustomer
{
    public sealed record UpdateCustomerCommand(
        Guid CustomerId,
        string Email,
        string Name) : ICommand;
}
