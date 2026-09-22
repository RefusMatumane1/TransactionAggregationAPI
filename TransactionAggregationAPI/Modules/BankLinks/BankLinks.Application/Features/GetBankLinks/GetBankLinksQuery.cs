using SharedKernel.Abstractions;
using Modules.BankLinks.Application.DTOs;

namespace Modules.BankLinks.Application.Features.GetBankLinks
{
    public sealed record GetBankLinksQuery(Guid CustomerId) : IQuery<IReadOnlyList<BankLinkDto>>;
}
