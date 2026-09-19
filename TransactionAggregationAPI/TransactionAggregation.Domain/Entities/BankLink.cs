using SharedKernel.Common;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Enums;
using SharedKernel.Exceptions;

namespace TransactionAggregation.Domain.Entities
{
    public sealed class BankLink : BaseEntity
    {
        private BankLink() { }

        public BankLinkId Id { get; private set; } = null!;
        public CustomerId CustomerId { get; private set; } = null!;
        public Institution Institution { get; private set; }
        public BankLinkStatus Status { get; private set; }

        public AccountId? AccountId { get; private set; }

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

        public void MarkNeedsReauthorization()
        {
            if (Status == BankLinkStatus.Revoked)
                return;

            Status = BankLinkStatus.NeedsReauthorization;
            UpdatedAt = DateTime.UtcNow;
        }

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

            EncryptedAccessToken = null;
            EncryptedRefreshToken = null;
            TokenExpiresAt = null;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}