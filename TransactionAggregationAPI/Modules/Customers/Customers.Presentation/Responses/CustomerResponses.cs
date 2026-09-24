using Modules.Customers.Application.DTOs;
using Modules.Customers.Domain.ValueObjects;

namespace Modules.Customers.Presentation.Responses
{
    public sealed record CustomerResponse(
        Guid Id,
        string Email,
        string Name,
        DateTime CreatedAt,
        DateTime? UpdatedAt = null)
    {
        internal static CustomerResponse From(CustomerDto customer) =>
            new(customer.Id, customer.Email, customer.Name, customer.CreatedAt, customer.UpdatedAt);
    }

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
        DateTime? UpdatedAt = null,
        decimal PendingBalance = 0,
        decimal AvailableBalance = 0)
    {
        internal static AccountResponse From(AccountDto account) => new(
            account.Id,
            account.CustomerId,
            account.AccountNumber,
            account.AccountName,
            account.AccountType,
            account.Balance,
            account.Currency,
            account.IsActive,
            account.CreatedAt,
            account.UpdatedAt,
            account.PendingBalance,
            account.AvailableBalance);
    }
}