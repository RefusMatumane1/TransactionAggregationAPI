using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Outbox;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Events.Transaction;
using TransactionAggregation.Domain.Outbox;

namespace TransactionAggregation.Application.Features.Transactions.Events
{
    public class TransactionCreatedEventHandler(
        ILogger<TransactionCreatedEventHandler> _logger,
        ITransactionCategorizationService _categorizationService,
        IApplicationDbContext _context)
        : INotificationHandler<TransactionCreatedDomainEvent>
    {
        public async Task Handle(TransactionCreatedDomainEvent notification, CancellationToken cancellationToken)
        {
            var transaction = notification.Transaction;

            _logger.LogInformation(
                "Transaction {TransactionId} created for customer {CustomerId}, Amount: {Amount}",
                transaction.Id.Value,
                transaction.CustomerId.Value,
                transaction.Amount.Formatted);

            if (transaction.Category == TransactionCategory.Uncategorized)
            {
                var category = await _categorizationService.CategorizeTransactionAsync(transaction, cancellationToken);
                transaction.Categorize(category, isAuto: true);
            }

            var payload = new TransactionCreatedOutboxPayload(transaction.Id.Value, transaction.CustomerId.Value);
            _context.OutboxMessages.Add(OutboxMessage.Create(
                OutboxMessageTypes.TransactionCreated, JsonSerializer.Serialize(payload)));
        }
    }
}