using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Commands.Customer.UpdateCustomer
{
    public sealed record UpdateCustomerCommand(
        Guid CustomerId,
        string Email,
        string Name) : ICommand;
}
