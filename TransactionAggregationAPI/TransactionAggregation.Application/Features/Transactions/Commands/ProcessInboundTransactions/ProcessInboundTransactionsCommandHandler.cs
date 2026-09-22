using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using SharedKernel.Abstractions;
using SharedKernel.Common.Interfaces;
using TransactionAggregation.Application.Common.Interfaces;
using SharedKernel.Common.Models;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using Modules.BankLinks.Contracts;
using TransactionAggregation.Application.Common.Outbox;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Common.ValueObjects;
using SharedKernel.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Features.Transactions.Commands.ProcessInboundTransactions
{
    internal sealed class ProcessInboundTransactionsCommandHandler(
        IApplicationDbContext context,
        IMessagingDbContext messaging,
        IBankLinksReadApi bankLinksReadApi,
        ITransactionCategorizationService categorizationService,
        ILogger<ProcessInboundTransactionsCommandHandler> logger)
        : ICommandHandler<ProcessInboundTransactionsCommand, int>
    {
        public async Task<Result<int>> Handle(ProcessInboundTransactionsCommand request, CancellationToken cancellationToken)
        {
            var link = await bankLinksReadApi.FindActiveLinkByExternalAccountIdAsync(
                request.ExternalAccountId, cancellationToken);

            if (link is null)
                return Result.Failure<int>(Error.NotFound("BankLink", request.ExternalAccountId));

            var linkCustomerId = CustomerId.CreateFrom(link.CustomerId);
            var linkAccountId = AccountId.CreateFrom(link.AccountId);

            var incomingExternalIds = request.Transactions.Select(t => t.Id).ToHashSet();

            var existingExternalIds = await context.Transactions
                                .Where(t => t.CustomerId == linkCustomerId && incomingExternalIds.Contains(t.Source.ExternalId))
                                .Select(t => t.Source.ExternalId)
                                .ToHashSetAsync(cancellationToken);

            var newTransactions = new List<Transaction>();

            foreach (var dto in request.Transactions)
            {
                if (existingExternalIds.Contains(dto.Id))
                    continue;

                var transaction = Transaction.Create(
                    linkCustomerId,
                    Money.Create(dto.Amount, dto.Currency),
                    dto.Description,
                    TransactionCategory.Uncategorized,
                    TransactionSource.Create(link.InstitutionName, dto.Id),
                    linkAccountId,
                    dto.Date);

                var category = await categorizationService.CategorizeTransactionAsync(transaction, cancellationToken);
                if (category != TransactionCategory.Uncategorized)
                    transaction.Categorize(category, isAuto: true);

                newTransactions.Add(transaction);
            }

            if (newTransactions.Count == 0)
                return Result.Success(0);

            await context.Transactions.AddRangeAsync(newTransactions, cancellationToken);

            foreach (var transaction in newTransactions)
            {
                var payload = new TransactionSyncedOutboxPayload(
                    transaction.Id.Value, transaction.CustomerId.Value, link.InstitutionName);
                messaging.OutboxMessages.Add(OutboxMessage.Create(
                    OutboxMessageTypes.TransactionSynced, JsonSerializer.Serialize(payload)));
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("23505") == true)
            {

                logger.LogWarning(
                                        "Duplicate transaction external id processing inbound transactions from source {SourceName} for BankLink {BankLinkId} — already inserted by a concurrent call",
                                        request.SourceName, link.BankLinkId);
                return Result.Success(0);
            }

            logger.LogInformation(
                "Ingested {Count} new transactions from source {SourceName} for BankLink {BankLinkId} ({Institution})",
                newTransactions.Count, request.SourceName, link.BankLinkId, link.InstitutionName);

            return Result.Success(newTransactions.Count);
        }
    }
}