using Microsoft.EntityFrameworkCore;
using Modules.BankLinks.Contracts;
using Modules.Customers.Application.Errors;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Domain.ValueObjects;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;

namespace Modules.Customers.Application.Adapters
{
    /// <summary>
    /// Implements BankLinks' IAccountProvisioningPort next to the Account entity, so BankLinks never
    /// depends on Customers' persistence.
    /// </summary>
    public sealed class AccountProvisioningAdapter(ICustomersDbContext _context) : IAccountProvisioningPort
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
                return Result.Failure<Guid>(CustomerErrors.NotFound(customerId));

            var existingAccount = await _context.Accounts
                .FirstOrDefaultAsync(
                    a => a.CustomerId == customerIdVo && a.AccountNumber == accountNumber,
                    cancellationToken);

            if (existingAccount is not null)
                return Result.Success(existingAccount.Id.Value);

            var account = Domain.Account.Create(
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