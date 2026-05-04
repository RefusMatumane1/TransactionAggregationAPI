using TransactionAggregation.Domain.Enums;

namespace TransactionAggregationAPI.DTOs.Account
{
    public sealed record AccountResponse(
        Guid Id,
        Guid CustomerId,
        string AccountNumber,
        string AccountName,
        AccountType AccountType,
        decimal Balance,
        string Currency,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt = null);
}
