using TransactionAggregation.Worker.Messaging;

namespace TransactionAggregation.Worker.Transactions
{
    public sealed class InboxOptions : DispatcherOptions
    {
        public const string SectionName = "Inbox";
    }
}