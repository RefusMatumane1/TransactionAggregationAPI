using Microsoft.EntityFrameworkCore;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;
using Modules.BankLinks.Application.Ports;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Adapters
{
    /// <summary>
    /// Implements the port BankLinks owns. Lives here (not in BankLinks) because it needs
    /// direct access to the Account entity and IApplicationDbContext — neither of which
    /// BankLinks should depend on. Calls Account.Create(...) directly instead of
    /// Customer.AddAccount(...): the latter is Customer's aggregate reaching into
    /// Account's own "create an account" use case, which is exactly the cross-module
    /// coupling this restructuring is fixing (see ADR-0001/ADR-0009). Wired to
    /// IAccountProvisioningPort in Program.cs.
    /// </summary>
    public sealed class AccountProvisioningAdapter(IApplicationDbContext _context) : IAccountProvisioningPort
    {
        public async Task<Result<Guid>> ProvisionOrGetAccountAsync(
            Guid customerId,
            string accountNumber,
            string accountName,
            string accountType,
            string currency,
            CancellationToken cancellationToken = default)
        {
            var customerIdVo = CustomerId.CreateFrom(customerId);

            var customerExists = await _context.Customers
                .AnyAsync(c => c.Id == customerIdVo, cancellationToken);

            if (!customerExists)
                return Result.Failure<Guid>(Error.NotFound("Customer", customerId));

            var existingAccount = await _context.Accounts
                .FirstOrDefaultAsync(
                    a => a.CustomerId == customerIdVo && a.AccountNumber == accountNumber,
                    cancellationToken);

            if (existingAccount is not null)
                return Result.Success(existingAccount.Id.Value);

            var account = Account.Create(
                customerIdVo,
                accountNumber,
                accountName,
                MapAccountType(accountType),
                currency);

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success(account.Id.Value);
        }

        private static AccountType MapAccountType(string aggregatorAccountType) =>
            aggregatorAccountType.Trim().ToLowerInvariant() switch
            {
                "savings" => AccountType.Savings,
                "credit" or "creditcard" or "credit_card" => AccountType.CreditCard,
                "investment" => AccountType.Investment,
                "loan" => AccountType.Loan,
                _ => AccountType.Checking
            };
    }
}
