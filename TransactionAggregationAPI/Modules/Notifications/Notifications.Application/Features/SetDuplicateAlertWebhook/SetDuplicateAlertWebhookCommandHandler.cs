using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.Notifications.Application.Common;
using Modules.Notifications.Application.Persistence;
using Modules.Notifications.Domain;
using Modules.WebhookSources.Contracts;
using SharedKernel.Common.Models;

namespace Modules.Notifications.Application.Features.SetDuplicateAlertWebhook
{
    internal sealed class SetDuplicateAlertWebhookCommandHandler(
        INotificationsDbContext context,
        IWebhookSourceDirectory banks,
        IAlertUrlProtector protector,
        IUserContext userContext,
        ILogger<SetDuplicateAlertWebhookCommandHandler> logger)
        : ICommandHandler<SetDuplicateAlertWebhookCommand>
    {
        public async Task<Result> Handle(SetDuplicateAlertWebhookCommand request, CancellationToken cancellationToken)
        {
            var bankCode = await banks.FindBankCodeAsync(request.BankCode, cancellationToken);
            if (bankCode is null)
                return Result.Failure(Error.NotFound("Bank", request.BankCode));

            var existing = await context.AlertWebhooks.FirstOrDefaultAsync(w => w.BankCode == bankCode, cancellationToken);
            var clearing = string.IsNullOrWhiteSpace(request.Url);

            if (clearing)
            {
                if (existing is not null)
                    context.AlertWebhooks.Remove(existing);
            }
            else
            {
                var protectedUrl = protector.Protect(request.Url!.Trim());
                if (existing is null)
                    context.AlertWebhooks.Add(AlertWebhook.Create(bankCode, protectedUrl));
                else
                    existing.Replace(protectedUrl);
            }

            // The address embeds a credential: the audit trail and the log record that it changed, never what it is.
            var detail = clearing ? "Duplicate-alert webhook cleared" : "Duplicate-alert webhook set";
            context.StageAudit([AdminAudit.Of(AuditEventTypes.DuplicateAlertWebhookChanged, bankCode, userContext.UserId, detail)]);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Bank {BankCode}: {Change} by admin {AdminId}", bankCode, detail, userContext.UserId);

            return Result.Success();
        }
    }
}