using SharedKernel.Common.Models;

namespace Modules.BankLinks.Contracts
{
    /// <summary>
    /// Owned by BankLinks: this module needs an Account to exist for a linked external
    /// bank account, but Accounts/Customers live in a different module (Modules.Customers).
    /// The adapter implementing this (Modules.Customers.Application.Adapters.AccountProvisioningAdapter,
    /// wired in Program.cs) is what actually creates/looks up the Account — BankLinks
    /// itself has no dependency on the Account entity or its persistence. Lives in
    /// Contracts rather than Application.Ports because a DIFFERENT module implements it —
    /// same rule as IBankLinksReadApi: a port whose implementation lives outside this
    /// module belongs in Contracts (the leaf, cross-module-safe project), not
    /// Application.Ports (reserved for ports this module's own Infrastructure implements,
    /// like IBankAggregatorClient/IBankLinkCredentialProtector).
    /// </summary>
    public interface IAccountProvisioningPort
    {
        Task<Result<Guid>> ProvisionOrGetAccountAsync(
            Guid customerId,
            string accountNumber,
            string accountName,
            string accountType,
            string currency,
            CancellationToken cancellationToken = default);
    }
}
