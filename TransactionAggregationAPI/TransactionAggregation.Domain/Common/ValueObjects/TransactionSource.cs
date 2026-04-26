using TransactionAggregation.Domain.Exceptions;

namespace TransactionAggregation.Domain.Common.ValueObjects
{
    public sealed class TransactionSource : ValueObject
    {
        public string Name { get; }
        public string ExternalId { get; }
        public string? Provider { get; }
        public string? Version { get; }
        public DateTime? LastSyncDate { get; private set; }

        private TransactionSource(
            string name,
            string externalId,
            string? provider = null,
            string? version = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Transaction source name cannot be empty");

            if (string.IsNullOrWhiteSpace(externalId))
                throw new DomainException("External ID cannot be empty");

            Name = name;
            ExternalId = externalId;
            Provider = provider;
            Version = version;
        }

        public static TransactionSource Create(
            string name,
            string externalId,
            string? provider = null,
            string? version = null)
        {
            return new TransactionSource(name, externalId, provider, version);
        }

        public static TransactionSource CreateFromBankA(string externalId) =>
            new("Bank A", externalId, "BankA Corp", "2.0");

        public static TransactionSource CreateFromBankB(string externalId) =>
            new("Bank B", externalId, "BankB Financial", "1.5");

        public static TransactionSource CreateFromWallet(string externalId) =>
            new("Digital Wallet", externalId, "WalletPay", "3.2");

        public void UpdateLastSyncDate(DateTime syncDate)
        {
            LastSyncDate = syncDate;
        }

        public bool IsOlderThan(TimeSpan age) =>
            LastSyncDate.HasValue && (DateTime.UtcNow - LastSyncDate.Value) > age;

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Name;
            yield return ExternalId;
        }

        public override string ToString() => $"{Name} ({ExternalId})";
    }
}
