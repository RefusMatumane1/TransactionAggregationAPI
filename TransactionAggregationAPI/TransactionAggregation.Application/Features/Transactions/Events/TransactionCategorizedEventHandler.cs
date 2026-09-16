using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Outbox;
using TransactionAggregation.Domain.Events.Transaction;
using TransactionAggregation.Domain.Outbox;

namespace TransactionAggregation.Application.Features.Transactions.Events
{
    public class TransactionCategorizedEventHandler(
        ILogger<TransactionCategorizedEventHandler> _logger,
        IApplicationDbContext _context)
        : INotificationHandler<TransactionCategorizedDomainEvent>
    {
        public Task Handle(TransactionCategorizedDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Transaction {TransactionId} recategorized from {OldCategory} to {NewCategory}. Auto: {IsAuto}",
                notification.Transaction.Id.Value,
                notification.OldCategory,
                notification.NewCategory,
                notification.IsAutoCategorized);

            // Cache invalidation/analytics/notification are outboxed rather than called in-line
            // here — see TransactionCreatedEventHandler for why (this also runs pre-save inside
            // SaveChangesAsync). The dispatcher decides whether to notify based on IsAutoCategorized.
            var payload = new TransactionCategorizedOutboxPayload(
                notification.Transaction.Id.Value,
                notification.Transaction.CustomerId.Value,
                notification.OldCategory,
                notification.NewCategory,
                notification.IsAutoCategorized);

            _context.OutboxMessages.Add(OutboxMessage.Create(
                OutboxMessageTypes.TransactionCategorized, JsonSerializer.Serialize(payload)));

            return Task.CompletedTask;
        }
    }
}
