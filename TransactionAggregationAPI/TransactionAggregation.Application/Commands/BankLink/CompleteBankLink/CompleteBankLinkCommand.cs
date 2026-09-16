using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Commands.BankLink.CompleteBankLink
{
    /// <summary>Handles the aggregator's OAuth redirect back to us; returns the linked AccountId.</summary>
    public sealed record CompleteBankLinkCommand(string Code, string State) : ICommand<Guid>;
}
