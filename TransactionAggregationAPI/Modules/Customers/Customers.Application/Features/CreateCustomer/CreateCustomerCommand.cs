using SharedKernel.Abstractions;
using SharedKernel.Common.Attributes;

namespace Modules.Customers.Application.Features.CreateCustomer
{
    public sealed record CreateCustomerCommand(
        string Email,
        string Name,
        [property: Sensitive] string Password) : ICommand<Guid>;
}
