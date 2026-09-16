using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Exceptions;

namespace TransactionAggregation.Domain.Entities
{
    /// <summary>
    /// A customer's consent to let the account-aggregator pull transactions from one
    /// external bank account. Access/refresh tokens are stored encrypted (see
    /// IBankLinkCredentialProtector) — this entity never holds them in plaintext.
    /// </summary>
    public sealed class BankLink : BaseEntity
    {
        private BankLink() { }

        public BankLinkId Id { get; private set; }
        public CustomerId CustomerId { get; private set; }
        public Institution Institution { get; private set; }
        public BankLinkStatus Status { get; private set; }

        /// <summary>The internal Account created to represent this externally-linked account.</summary>
        public AccountId? AccountId { get; private set; }

        /// <summary>The aggregator's own id for the linked account — needed to scope transaction pulls.</summary>
        public string? ExternalAccountId { get; private set; }

        public string? EncryptedAccessToken { get; private set; }
        public string? EncryptedRefreshToken { get; private set; }
        public DateTime? TokenExpiresAt { get; private set; }

        public static BankLink Create(CustomerId customerId, Institution institution)
        {
            return new BankLink
            {
                Id = BankLinkId.Create(),
                CustomerId = customerId,
                Institution = institution,
                Status = BankLinkStatus.PendingAuthorization
            };
        }

        /// <summary>Called once the customer completes consent and we've exchanged the auth code for tokens.</summary>
        public void Activate(
            AccountId accountId,
            string externalAccountId,
            string encryptedAccessToken,
            string encryptedRefreshToken,
            DateTime tokenExpiresAt)
        {
            if (Status is BankLinkStatus.Revoked)
                throw new DomainException("Cannot activate a revoked bank link — create a new one instead");

            AccountId = accountId;
            ExternalAccountId = externalAccountId;
            EncryptedAccessToken = encryptedAccessToken;
            EncryptedRefreshToken = encryptedRefreshToken;
            TokenExpiresAt = tokenExpiresAt;
            Status = BankLinkStatus.Active;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Called after a successful refresh-token exchange.</summary>
        public void UpdateTokens(string encryptedAccessToken, string encryptedRefreshToken, DateTime tokenExpiresAt)
        {
            if (Status is BankLinkStatus.Revoked)
                throw new DomainException("Cannot update tokens on a revoked bank link");

            EncryptedAccessToken = encryptedAccessToken;
            EncryptedRefreshToken = encryptedRefreshToken;
            TokenExpiresAt = tokenExpiresAt;
            Status = BankLinkStatus.Active;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>The bank rejected the token (expired/revoked on their side) — customer must re-consent.</summary>
        public void MarkNeedsReauthorization()
        {
            if (Status == BankLinkStatus.Revoked)
                return;

            Status = BankLinkStatus.NeedsReauthorization;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Re-initiates consent on an existing (Revoked or NeedsReauthorization) link,
        /// rather than creating a duplicate row — CustomerId+Institution is unique.</summary>
        public void ResetForReauthorization()
        {
            if (Status is BankLinkStatus.Active or BankLinkStatus.PendingAuthorization)
                throw new DomainException($"Bank link is already {Status} — nothing to reauthorize");

            Status = BankLinkStatus.PendingAuthorization;
            EncryptedAccessToken = null;
            EncryptedRefreshToken = null;
            TokenExpiresAt = null;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Revoke()
        {
            Status = BankLinkStatus.Revoked;
            // Don't let a revoked link's tokens linger in the database.
            EncryptedAccessToken = null;
            EncryptedRefreshToken = null;
            TokenExpiresAt = null;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
