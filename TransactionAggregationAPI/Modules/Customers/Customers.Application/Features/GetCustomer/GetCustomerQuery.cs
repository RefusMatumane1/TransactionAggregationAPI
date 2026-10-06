using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using FluentValidation;
using Modules.Customers.Application.DTOs;

namespace Modules.Customers.Application.Features.GetCustomer
{
    public sealed record GetCustomerQuery(Guid CustomerId, InstitutionAccess Access) : IQuery<CustomerDto>;

    public sealed class GetCustomerQueryValidator : AbstractValidator<GetCustomerQuery>
    {
        public GetCustomerQueryValidator() => RuleFor(x => x.Access).NotNull();
    }
}