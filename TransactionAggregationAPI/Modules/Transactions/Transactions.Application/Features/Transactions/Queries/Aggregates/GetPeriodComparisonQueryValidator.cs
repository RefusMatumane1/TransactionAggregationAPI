using FluentValidation;
using Modules.Transactions.Application.Common.Aggregation;

namespace Modules.Transactions.Application.Features.Transactions.Queries.Aggregates
{
    public sealed class GetPeriodComparisonQueryValidator : AbstractValidator<GetPeriodComparisonQuery>
    {
        public GetPeriodComparisonQueryValidator()
        {
            RuleFor(x => x.Filter).NotNull().SetValidator(new TransactionFilterValidator());
            RuleFor(x => x.Currency).IsoCurrency();
            this.ValidPeriod(x => x.From, x => x.To);
        }
    }
}