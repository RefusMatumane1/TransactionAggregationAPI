using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Application.Common.Outbox;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Outbox;

namespace TransactionAggregation.Application.Features.Transactions.Commands.ProcessInboundTransactions
{
    internal sealed class ProcessInboundTransactionsCommandHandler(
        IApplicationDbContext context,
        ITransactionCategorizationService categorizationService,
        ILogger<ProcessInboundTransactionsCommandHandler> logger)
        : ICommandHandler<ProcessInboundTransactionsCommand, int>
    {
        public async Task<Result<int>> Handle(ProcessInboundTransactionsCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var link = await context.BankLinks
                    .FirstOrDefaultAsync(
                        b => b.ExternalAccountId == request.ExternalAccountId && b.Status == BankLinkStatus.Active,
                        cancellationToken);

                if (link is null)
                    return Result.Failure<int>(Error.NotFound("BankLink", request.ExternalAccountId));

                var incomingExternalIds = request.Transactions.Select(t => t.Id).ToHashSet();

                var existingExternalIds = await context.Transactions
                                    .Where(t => t.CustomerId == link.CustomerId && incomingExternalIds.Contains(t.Source.ExternalId))
                                    .Select(t => t.Source.ExternalId)
                                    .ToHashSetAsync(cancellationToken);

                var newTransactions = new List<Transaction>();

                foreach (var dto in request.Transactions)
                {
                    if (existingExternalIds.Contains(dto.Id))
                        continue;

                    var transaction = Transaction.Create(
                        link.CustomerId,
                        Money.Create(dto.Amount, dto.Currency),
                        dto.Description,
                        TransactionCategory.Uncategorized,
                        TransactionSource.Create(link.Institution.ToString(), dto.Id),
                        link.AccountId,
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
                        transaction.Id.Value, transaction.CustomerId.Value, link.Institution.ToString());
                    context.OutboxMessages.Add(OutboxMessage.Create(
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
                                            request.SourceName, link.Id.Value);
                    return Result.Success(0);
                }

                logger.LogInformation(
                    "Ingested {Count} new transactions from source {SourceName} for BankLink {BankLinkId} ({Institution})",
                    newTransactions.Count, request.SourceName, link.Id.Value, link.Institution);

                return Result.Success(newTransactions.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error processing inbound transactions from source {SourceName} for external account {ExternalAccountId}",
                    request.SourceName, request.ExternalAccountId);
                return Result.Failure<int>(Error.Unexpected);
            }
        }
    }
}