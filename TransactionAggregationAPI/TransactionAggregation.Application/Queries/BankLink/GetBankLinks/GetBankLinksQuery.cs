using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Queries.BankLink.GetBankLinks
{
    public sealed record GetBankLinksQuery(Guid CustomerId) : IQuery<IReadOnlyList<BankLinkDto>>;
}
