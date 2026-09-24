using Modules.BankLinks.Application.DTOs;
using SharedKernel.Abstractions;

namespace Modules.BankLinks.Application.Features.GetBankLinks
{
    public sealed record GetBankLinksQuery(Guid CustomerId) : IQuery<IReadOnlyList<BankLinkDto>>;
}