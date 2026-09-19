using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Outbox;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Events.Transaction;

namespace TransactionAggregation.Application.Features.Transactions.Events
{
    public class TransactionCreatedEventHandler(
        ILogger<TransactionCreatedEventHandler> _logger,
        ITransactionCategorizationService _categorizationService,
        IMessagingDbContext _messaging)
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
            _messaging.OutboxMessages.Add(OutboxMessage.Create(
                OutboxMessageTypes.TransactionCreated, JsonSerializer.Serialize(payload)));
        }
    }
}
