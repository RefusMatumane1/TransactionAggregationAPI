using Modules.Customers.Domain.ValueObjects;

namespace Modules.Customers.Application.DTOs
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
        DateTime? UpdatedAt = null,
        decimal PendingBalance = 0,
        decimal AvailableBalance = 0);
}