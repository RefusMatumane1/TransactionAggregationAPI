using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Commands.BankLink.InitiateBankLink;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Exceptions;

namespace TransactionAggregation.Application.Commands.BankLink.CompleteBankLink
{
    internal sealed class CompleteBankLinkCommandHandler(
        IApplicationDbContext _context,
        IBankAggregatorClient _client,
        IBankLinkCredentialProtector _protector,
        IDistributedCache _cache,
        ILogger<CompleteBankLinkCommandHandler> logger)
        : ICommandHandler<CompleteBankLinkCommand, Guid>
    {
        public async Task<Result<Guid>> Handle(CompleteBankLinkCommand request, CancellationToken cancellationToken)
        {
            var cacheKey = InitiateBankLinkCommandHandler.StateCacheKey(request.State);
            var statePayloadJson = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (statePayloadJson is null)
                return Result.Failure<Guid>(Error.Validation("Authorization state is invalid or has expired. Please try linking again."));

            await _cache.RemoveAsync(cacheKey, cancellationToken);

            var statePayload = JsonSerializer.Deserialize<InitiateBankLinkCommandHandler.StatePayload>(statePayloadJson)!;
            var customerId = CustomerId.CreateFrom(statePayload.CustomerId);

            var link = await _context.BankLinks
                .FirstOrDefaultAsync(
                    b => b.CustomerId == customerId && b.Institution == statePayload.Institution,
                    cancellationToken);

            if (link is not { Status: BankLinkStatus.PendingAuthorization })
                return Result.Failure<Guid>(Error.Validation("No pending bank link found for this authorization."));

            var customer = await _context.Customers
                .Include(c => c.Accounts)
                .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

            if (customer is null)
                return Result.Failure<Guid>(Error.NotFound("Customer", statePayload.CustomerId));

            var tokens = await _client.ExchangeAuthorizationCodeAsync(request.Code, cancellationToken);
            var linkedAccount = await _client.GetLinkedAccountAsync(tokens.AccessToken, cancellationToken);

            Domain.Entities.Account account;
            try
            {
                account = customer.AddAccount(
                    linkedAccount.AccountNumber,
                    linkedAccount.AccountName,
                    MapAccountType(linkedAccount.AccountType),
                    linkedAccount.Currency);
            }
            catch (DomainException)
            {

                account = customer.Accounts.First(a => a.AccountNumber == linkedAccount.AccountNumber);
            }

            link.Activate(
                account.Id,
                linkedAccount.ExternalAccountId,
                _protector.Protect(tokens.AccessToken),
                _protector.Protect(tokens.RefreshToken),
                tokens.ExpiresAtUtc);

            await _context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Bank link completed for customer {CustomerId}, institution {Institution}, account {AccountId}",
                statePayload.CustomerId, statePayload.Institution, account.Id.Value);

            return Result.Success(account.Id.Value);
        }

        private static Domain.Enums.AccountType MapAccountType(string aggregatorAccountType) =>
            aggregatorAccountType.Trim().ToLowerInvariant() switch
            {
                "savings" => Domain.Enums.AccountType.Savings,
                "credit" or "creditcard" or "credit_card" => Domain.Enums.AccountType.CreditCard,
                "investment" => Domain.Enums.AccountType.Investment,
                "loan" => Domain.Enums.AccountType.Loan,
                _ => Domain.Enums.AccountType.Checking
            };
    }
}