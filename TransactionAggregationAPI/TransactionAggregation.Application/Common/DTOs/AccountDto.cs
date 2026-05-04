using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.DTOs
{
    public record AccountDto(
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
