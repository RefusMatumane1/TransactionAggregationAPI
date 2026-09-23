using Modules.Customers.Domain.ValueObjects;

namespace Modules.Customers.Infrastructure.Endpoints
{
    public sealed record CreateCustomerRequest(
        string Email,
        string Name, string password);

    public sealed record UpdateCustomerRequest(
        string Email,
        string Name);

    public sealed record CustomerResponse(
        Guid Id,
        string Email,
        string Name,
        DateTime CreatedAt,
        DateTime? UpdatedAt = null);

    public sealed record CreateAccountRequest(
        string AccountNumber,
        string AccountName,
        AccountType AccountType,
        string Currency = "ZAR");

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
