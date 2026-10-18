using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.Notifications.Application.DTOs;
using Modules.Notifications.Application.Persistence;
using Modules.WebhookSources.Contracts;
using SharedKernel.Common.Models;

namespace Modules.Notifications.Application.Features.GetDuplicateAlertWebhook
{
    internal sealed class GetDuplicateAlertWebhookQueryHandler(INotificationsDbContext context, IWebhookSourceDirectory banks)
        : IQueryHandler<GetDuplicateAlertWebhookQuery, DuplicateAlertWebhookDto>
    {
        public async Task<Result<DuplicateAlertWebhookDto>> Handle(GetDuplicateAlertWebhookQuery request, CancellationToken cancellationToken)
        {
            var bankCode = await banks.FindBankCodeAsync(request.BankCode, cancellationToken);
            if (bankCode is null)
                return Result.Failure<DuplicateAlertWebhookDto>(Error.NotFound("Bank", request.BankCode));

            var updatedAt = await context.AlertWebhooks
                .AsNoTracking()
                .Where(w => w.BankCode == bankCode)
                .Select(w => (DateTime?)w.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return Result.Success(new DuplicateAlertWebhookDto(bankCode, updatedAt is not null, updatedAt));
        }
    }
}