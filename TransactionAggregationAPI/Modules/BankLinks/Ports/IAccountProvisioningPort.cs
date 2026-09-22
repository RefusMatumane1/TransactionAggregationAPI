using SharedKernel.Common.Models;

namespace Modules.BankLinks.Ports
{
    /// <summary>
    /// Owned by BankLinks: this module needs an Account to exist for a linked external
    /// bank account, but Accounts/Customers haven't been extracted into their own module
    /// yet. The adapter implementing this (in TransactionAggregation.Application, wired
    /// in Program.cs) is what actually creates/looks up the Account — BankLinks itself
    /// has no dependency on the Account entity or its persistence.
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
