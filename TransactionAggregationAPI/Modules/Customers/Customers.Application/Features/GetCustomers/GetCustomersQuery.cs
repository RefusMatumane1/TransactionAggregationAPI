using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Application.Pagination;
using FluentValidation;
using Modules.Customers.Application.DTOs;
using Modules.Customers.Domain;
using Modules.Customers.Domain.ValueObjects;

namespace Modules.Customers.Application.Features.GetCustomers
{
    // Customers the caller can see, by name. Search matches the name or the reference.
    public sealed record GetCustomersQuery(InstitutionAccess Access, string? Search = null, string? Cursor = null, int PageSize = GetCustomersQuery.DefaultPageSize)
        : IQuery<CursorPage<CustomerSummaryDto>>
    {
        public const int DefaultPageSize = 50;
        public const int MaxPageSize = 100;
        public const int MinSearchLength = 2;
        public const int MaxSearchLength = 100;
    }

    public static class CustomerSort
    {
        public static readonly KeysetSort<Customer> ByName =
            new KeysetSort<Customer, string, CustomerId>(
                "name", c => c.Name, c => c.Id, id => id.Value, CustomerId.CreateFrom, KeyCodecs.String);
    }

    public sealed class GetCustomersQueryValidator : AbstractValidator<GetCustomersQuery>
    {
        public GetCustomersQueryValidator()
        {
            RuleFor(x => x.Access).NotNull();
            RuleFor(x => x.PageSize).InclusiveBetween(1, GetCustomersQuery.MaxPageSize).OverridePropertyName("pageSize");
            RuleFor(x => x.Cursor).MustBeACursorFor(CustomerSort.ByName, descending: false).OverridePropertyName("cursor");
            RuleFor(x => x.Search!.Trim())
                .Length(GetCustomersQuery.MinSearchLength, GetCustomersQuery.MaxSearchLength)
                .When(x => !string.IsNullOrWhiteSpace(x.Search))
                .OverridePropertyName("search");
        }
    }
}