using Modules.BankLinks.ValueObjects;

namespace Modules.BankLinks.DTOs
{
    public sealed record BankLinkDto(
        Guid Id,
        Institution Institution,
        BankLinkStatus Status,
        Guid? AccountId,
        DateTime LinkedAt);
}
