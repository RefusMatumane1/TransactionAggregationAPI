using TransactionAggregation.Domain.Enums;

namespace TransactionAggregationAPI.DTOs
{
    public sealed record CategorizeTransactionRequest(
        TransactionCategory Category);
}
