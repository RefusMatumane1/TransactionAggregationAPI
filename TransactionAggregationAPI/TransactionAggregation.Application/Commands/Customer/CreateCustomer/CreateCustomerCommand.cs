using SharedKernel.Abstractions;
using SharedKernel.Common.Attributes;

namespace TransactionAggregation.Application.Commands.Customer.CreateCustomer
{
    public sealed record CreateCustomerCommand(
        string Email,
        string Name,
        [property: Sensitive] string Password) : ICommand<Guid>;
}