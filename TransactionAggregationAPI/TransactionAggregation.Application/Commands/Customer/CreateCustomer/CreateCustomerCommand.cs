using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Commands.Customer.CreateCustomer
{
    public sealed record CreateCustomerCommand(
        string Email,
        string Name,
        string Password) : ICommand<Guid>;
}
