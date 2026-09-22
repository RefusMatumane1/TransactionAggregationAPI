using SharedKernel.Abstractions;
using Modules.BankLinks.DTOs;

namespace Modules.BankLinks.Features.GetBankLinks
{
    public sealed record GetBankLinksQuery(Guid CustomerId) : IQuery<IReadOnlyList<BankLinkDto>>;
}
