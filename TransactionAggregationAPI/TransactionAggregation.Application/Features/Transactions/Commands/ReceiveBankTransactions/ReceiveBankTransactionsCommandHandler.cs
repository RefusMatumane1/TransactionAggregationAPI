using System.Text.Json;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Inbox;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Inbox;

namespace TransactionAggregation.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    internal sealed class ReceiveBankTransactionsCommandHandler(IApplicationDbContext context)
        : ICommandHandler<ReceiveBankTransactionsCommand, Guid>
    {
        public async Task<Result<Guid>> Handle(ReceiveBankTransactionsCommand request, CancellationToken cancellationToken)
        {

            var payload = new InboundTransactionsPayload(request.ExternalAccountId, request.Transactions);
            var inboxMessage = InboxMessage.Create(request.SourceName, JsonSerializer.Serialize(payload));

            context.InboxMessages.Add(inboxMessage);
            await context.SaveChangesAsync(cancellationToken);

            return Result.Success(inboxMessage.Id.Value);
        }
    }
}