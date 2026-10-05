using FluentValidation;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransaction
{
    public sealed class GetTransactionQueryValidator : AbstractValidator<GetTransactionQuery>
    {
        public GetTransactionQueryValidator()
        {
            RuleFor(x => x.Access).NotNull();
        }
    }
}