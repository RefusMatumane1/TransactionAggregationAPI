using System.Text.Json;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Persistence;
using TransactionAggregation.Application.Common.Inbox;

namespace TransactionAggregation.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    internal sealed class ReceiveBankTransactionsCommandHandler(IMessagingDbContext messaging)
        : ICommandHandler<ReceiveBankTransactionsCommand, Guid>
    {
        public async Task<Result<Guid>> Handle(ReceiveBankTransactionsCommand request, CancellationToken cancellationToken)
        {

            var payload = new InboundTransactionsPayload(request.ExternalAccountId, request.Transactions);
            var inboxMessage = InboxMessage.Create(request.SourceName, JsonSerializer.Serialize(payload));

            messaging.InboxMessages.Add(inboxMessage);
            await messaging.SaveChangesAsync(cancellationToken);

            return Result.Success(inboxMessage.Id.Value);
        }
    }
}
