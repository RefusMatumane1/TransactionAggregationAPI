using BuildingBlocks.Application.Querying;
using BuildingBlocks.Application.Validation;
using FluentValidation;

namespace Modules.Reporting.Application.Features.Aggregates
{
    public sealed class GetInstitutionBreakdownQueryValidator : AbstractValidator<GetInstitutionBreakdownQuery>
    {
        public GetInstitutionBreakdownQueryValidator()
        {
            RuleFor(x => x.Filter).NotNull().SetValidator(new AccountFilterValidator());
            RuleFor(x => x.Currency!).CurrencyCodeFormat().When(x => x.Currency is not null);
            RuleFor(x => x.ConvertTo!).CurrencyCodeFormat().WithMessage("convertTo must be a 3-letter upper-case currency code, e.g. ZAR.").When(x => x.ConvertTo is not null).OverridePropertyName("convertTo");
            this.ValidPeriod(x => x.From, x => x.To);
        }
    }
}