using Modules.Customers.Application.DTOs;
using Modules.Customers.Application.Features.CreateCustomer;
using Modules.Customers.Application.Features.LinkAccount;

namespace Modules.Customers.Presentation
{
    public sealed record CreateCustomerRequest(string? Reference, string? Name)
    {
        internal CreateCustomerCommand ToCommand() => new(Reference?.Trim() ?? string.Empty, Name ?? string.Empty);
    }

    public sealed record LinkAccountRequest(string? Institution, string? ExternalAccountId)
    {
        internal LinkAccountCommand ToCommand(Guid customerId) =>
            new(customerId, Institution?.Trim() ?? string.Empty, ExternalAccountId ?? string.Empty);
    }

    public sealed record LinkedAccountResponse(string Institution, string ExternalAccountId, DateTime LinkedAt);

    public sealed record CustomerResponse(Guid Id, string Reference, string Name, DateTime CreatedAt, IReadOnlyList<LinkedAccountResponse> Accounts)
    {
        internal static CustomerResponse From(CustomerDto dto) => new(
            dto.Id, dto.Reference, dto.Name, dto.CreatedAt,
            dto.Accounts.Select(a => new LinkedAccountResponse(a.Institution, a.ExternalAccountId, a.LinkedAt)).ToList());
    }

    public sealed record CustomerSummaryResponse(Guid Id, string Reference, string Name, int AccountCount, IReadOnlyList<string> Institutions)
    {
        internal static CustomerSummaryResponse From(CustomerSummaryDto dto) =>
            new(dto.Id, dto.Reference, dto.Name, dto.AccountCount, dto.Institutions);
    }
}