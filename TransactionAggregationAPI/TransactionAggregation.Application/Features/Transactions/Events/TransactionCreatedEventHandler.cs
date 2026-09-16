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

            // Auto-categorize only if nothing has categorized this transaction already (change
            // is tracked by EF Core and saved after this handler returns). This event fires on
            // every Transaction.Create unconditionally, so without this check it would silently
            // overwrite a category the caller deliberately set — e.g. SeedData's hand-picked
            // categories, or ReceiveBankTransactionsCommandHandler's own pre-save keyword match
            // — with Uncategorized whenever the keyword list didn't happen to also match the
            // description.
            if (transaction.Category == TransactionCategory.Uncategorized)
            {
                var category = await _categorizationService.CategorizeTransactionAsync(transaction, cancellationToken);
                transaction.Categorize(category, isAuto: true);
            }

            // Analytics/notification are outboxed rather than called in-line here: this handler
            // runs pre-save inside ApplicationDbContext.SaveChangesAsync, so a failing analytics
            // or notification call would abort an otherwise-valid transaction save. Enqueueing
            // is added to the same ChangeTracker/transaction, so it's as durable as the save itself.
            var payload = new TransactionCreatedOutboxPayload(transaction.Id.Value, transaction.CustomerId.Value);
            _context.OutboxMessages.Add(OutboxMessage.Create(
                OutboxMessageTypes.TransactionCreated, JsonSerializer.Serialize(payload)));
        }
    }
}
