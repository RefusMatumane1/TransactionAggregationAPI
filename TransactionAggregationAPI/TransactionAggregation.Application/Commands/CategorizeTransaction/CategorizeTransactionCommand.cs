using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Commands.CategorizeTransaction
{
    public sealed record CategorizeTransactionCommand(Guid TransactionId, TransactionCategory Category) : ICommand;
}
