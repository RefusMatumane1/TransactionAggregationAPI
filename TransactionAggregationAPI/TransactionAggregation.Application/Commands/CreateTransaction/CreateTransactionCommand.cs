using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Commands.CreateTransaction
{
    public sealed record CreateTransactionCommand(
        Guid CustomerId,
        decimal Amount,
        string Currency,
        DateTime TransactionDate,
        string Description,
        string SourceSystem,
        Guid? AccountId = null) : ICommand<Guid>;
}
