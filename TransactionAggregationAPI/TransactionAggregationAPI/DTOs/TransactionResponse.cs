using TransactionAggregation.Domain.Enums;

namespace TransactionAggregationAPI.DTOs
{
    public sealed record TransactionResponse(
        Guid Id,
        Guid CustomerId,
        Guid? AccountId,
        decimal Amount,
        string Currency,
        DateTime TransactionDate,
        string Description,
        TransactionCategory Category,
        TransactionStatus Status,
        string SourceSystem,
        DateTime CreatedAt,
        DateTime? UpdatedAt = null);
}
