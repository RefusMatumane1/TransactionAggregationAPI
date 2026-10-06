using Modules.Customers.Domain.ValueObjects;
using SharedKernel.Common;
using SharedKernel.Exceptions;
using System.Text.RegularExpressions;

namespace Modules.Customers.Domain
{
    // The person whose money it is. A customer holds accounts at several banks; linking an account
    // (bank code + the bank's account id, exactly as transactions carry them) is how the platform
    // aggregates one person's transactions across banks. Transactions themselves never name the
    // customer: the ledger is insert-only and links change, so ownership is resolved at read time.
    public sealed class Customer : BaseEntity
    {
        public const int MaxReferenceLength = 50;
        public const int MaxNameLength = 200;
        public const int MaxInstitutionLength = 50;
        public const int MaxExternalAccountIdLength = 200;

        // Bounds the per-request account filter a customer view expands into.
        public const int MaxLinkedAccounts = 50;

        private static readonly Regex CodePattern = new("^[A-Za-z0-9][A-Za-z0-9_-]*$", RegexOptions.Compiled);

        private readonly List<LinkedAccount> _accounts = [];

        private Customer() { }

        public CustomerId Id { get; private set; } = null!;

        // The organisation's own customer number (e.g. "CUST-0001"): unique, immutable, and safe to
        // log, unlike the name.
        public string Reference { get; private set; } = null!;

        public string Name { get; private set; } = null!;

        public IReadOnlyList<LinkedAccount> Accounts => _accounts;

        public static bool IsValidReference(string? reference) =>
            !string.IsNullOrEmpty(reference) && reference.Length <= MaxReferenceLength && CodePattern.IsMatch(reference);

        public static bool IsValidInstitution(string? institution) =>
            !string.IsNullOrEmpty(institution) && institution.Length <= MaxInstitutionLength && CodePattern.IsMatch(institution);

        public static bool IsValidExternalAccountId(string? accountId) =>
            !string.IsNullOrWhiteSpace(accountId)
            && accountId.Length <= MaxExternalAccountIdLength
            && accountId == accountId.Trim()
            && !accountId.Any(char.IsControl);

        public static Customer Create(string reference, string name)
        {
            if (!IsValidReference(reference))
                throw new DomainException(
                    $"A customer reference must be 1-{MaxReferenceLength} letters, digits, '-' or '_', starting with a letter or digit.");

            var customer = new Customer { Id = CustomerId.Create(), Reference = reference };
            customer.Rename(name);
            return customer;
        }

        public void Rename(string name)
        {
            var trimmed = name?.Trim() ?? string.Empty;
            if (trimmed.Length == 0 || trimmed.Length > MaxNameLength)
                throw new DomainException($"A customer name must be 1-{MaxNameLength} characters.");

            Name = trimmed;
            UpdatedAt = DateTime.UtcNow;
        }

        // Returns false when the account is already linked: linking is idempotent.
        public bool Link(string institution, string externalAccountId, DateTime now)
        {
            if (!IsValidInstitution(institution))
                throw new DomainException($"A bank code must be 1-{MaxInstitutionLength} letters, digits, '-' or '_'.");
            if (!IsValidExternalAccountId(externalAccountId))
                throw new DomainException($"An account id must be 1-{MaxExternalAccountIdLength} characters, without surrounding spaces.");

            if (FindAccount(institution, externalAccountId) is not null)
                return false;
            if (_accounts.Count >= MaxLinkedAccounts)
                throw new DomainException($"A customer can have at most {MaxLinkedAccounts} linked accounts.");

            _accounts.Add(new LinkedAccount(institution, externalAccountId, now));
            UpdatedAt = now;
            return true;
        }

        // Returns false when the account was not linked.
        public bool Unlink(string institution, string externalAccountId)
        {
            var account = FindAccount(institution, externalAccountId);
            if (account is null)
                return false;

            _accounts.Remove(account);
            UpdatedAt = DateTime.UtcNow;
            return true;
        }

        // Bank codes compare case-insensitively (as everywhere else); account ids exactly, as banks send them.
        private LinkedAccount? FindAccount(string institution, string externalAccountId) =>
            _accounts.FirstOrDefault(a =>
                string.Equals(a.Institution, institution, StringComparison.OrdinalIgnoreCase)
                && a.ExternalAccountId == externalAccountId);
    }

    // One bank account the customer has linked: the same (bank, account id) pair every transaction carries.
    public sealed class LinkedAccount
    {
        private LinkedAccount() { }

        internal LinkedAccount(string institution, string externalAccountId, DateTime linkedAt)
        {
            Institution = institution;
            ExternalAccountId = externalAccountId;
            LinkedAt = linkedAt;
        }

        public string Institution { get; private set; } = null!;
        public string ExternalAccountId { get; private set; } = null!;
        public DateTime LinkedAt { get; private set; }
    }
}