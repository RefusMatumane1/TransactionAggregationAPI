using Modules.BankLinks.Domain.ValueObjects;

namespace Modules.BankLinks.Application.DTOs
{
    public sealed record BankLinkDto(
        Guid Id,
        Institution Institution,
        BankLinkStatus Status,
        Guid? AccountId,
        DateTime LinkedAt);
}