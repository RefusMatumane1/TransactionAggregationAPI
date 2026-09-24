using SharedKernel.Common.Models;

namespace Modules.BankLinks.Contracts
{
    /// <summary>
    /// Owned by BankLinks, implemented by Customers (AccountProvisioningAdapter): BankLinks needs an
    /// Account for each linked bank account without depending on the Customers module. It lives in
    /// Contracts because another module implements it (ADR-0010).
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