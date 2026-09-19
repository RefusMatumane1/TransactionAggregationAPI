using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using TransactionAggregation.Application.Common.Outbox;
using TransactionAggregation.Domain.Events.Transaction;

namespace TransactionAggregation.Application.Features.Transactions.Events
{
    public class TransactionCategorizedEventHandler(
        ILogger<TransactionCategorizedEventHandler> _logger,
        IMessagingDbContext _messaging)
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

            var payload = new TransactionCategorizedOutboxPayload(
                            notification.Transaction.Id.Value,
                            notification.Transaction.CustomerId.Value,
                            notification.OldCategory,
                            notification.NewCategory,
                            notification.IsAutoCategorized);

            _messaging.OutboxMessages.Add(OutboxMessage.Create(
                OutboxMessageTypes.TransactionCategorized, JsonSerializer.Serialize(payload)));

            return Task.CompletedTask;
        }
    }
}
