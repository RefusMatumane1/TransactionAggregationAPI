using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Commands.BankLink.InitiateBankLink
{
    /// <summary>Starts the consent flow for one institution; returns the URL to redirect the
    /// customer's browser to.</summary>
    public sealed record InitiateBankLinkCommand(Guid CustomerId, Institution Institution) : ICommand<string>;
}
