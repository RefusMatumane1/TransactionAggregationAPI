using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.DTOs
{
    public sealed record BankLinkDto(
        Guid Id,
        Institution Institution,
        BankLinkStatus Status,
        Guid? AccountId,
        DateTime LinkedAt);
}
