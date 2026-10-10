using BuildingBlocks.Application.Abstractions.Authentication;
using Modules.Customers.Application.Common;
using Modules.Customers.Domain;

namespace Modules.Customers.Application.DTOs
{
    public sealed record LinkedAccountDto(string Institution, string ExternalAccountId, DateTime LinkedAt);

    public sealed record CustomerDto(Guid Id, string Reference, string Name, DateTime CreatedAt, IReadOnlyList<LinkedAccountDto> Accounts)
    {
   
        internal static CustomerDto From(Customer customer, InstitutionAccess access) => new(
            customer.Id.Value,
            customer.Reference,
            customer.Name,
            customer.CreatedAt,
            customer.Accounts
                .Where(a => access.Includes(a.Institution))
                .OrderBy(a => a.Institution, StringComparer.Ordinal)
                .ThenBy(a => a.ExternalAccountId, StringComparer.Ordinal)
                .Select(a => new LinkedAccountDto(a.Institution, a.ExternalAccountId, a.LinkedAt))
                .ToList());
    }

    public sealed record CustomerSummaryDto(Guid Id, string Reference, string Name, int AccountCount, IReadOnlyList<string> Institutions);
}