using BuildingBlocks.Application.Abstractions;
using FluentValidation;
using Modules.Customers.Application.Common;
using Modules.Customers.Application.DTOs;

namespace Modules.Customers.Application.Features.CreateCustomer
{
    public sealed record CreateCustomerCommand(string Reference, string Name) : ICommand<CustomerDto>;

    public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
    {
        public CreateCustomerCommandValidator()
        {
            RuleFor(x => x.Reference).CustomerReference().OverridePropertyName("reference");
            RuleFor(x => x.Name).CustomerName().OverridePropertyName("name");
        }
    }
}