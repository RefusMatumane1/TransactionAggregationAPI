using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Commands.Customer.DeleteCustomer
{
    public sealed record DeleteCustomerCommand(Guid CustomerId) : ICommand;
}
