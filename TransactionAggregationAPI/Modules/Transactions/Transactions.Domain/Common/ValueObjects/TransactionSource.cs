using SharedKernel.Common.ValueObjects;
using SharedKernel.Exceptions;

namespace Modules.Transactions.Domain.Common.ValueObjects
{
    public sealed class TransactionSource : ValueObject
    {
        public string Name { get; }
        public string ExternalId { get; }

        public const int MaxNameLength = 50;
        public const int MaxExternalIdLength = 100;

        private TransactionSource(string name, string externalId)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
                throw new DomainException($"Transaction source name must be 1-{MaxNameLength} characters");

            if (string.IsNullOrWhiteSpace(externalId) || externalId.Length > MaxExternalIdLength)
                throw new DomainException($"External ID must be 1-{MaxExternalIdLength} characters");

            Name = name;
            ExternalId = externalId;
        }

        public static TransactionSource Create(string name, string externalId) => new(name, externalId);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Name;
            yield return ExternalId;
        }

        public override string ToString() => $"{Name} ({ExternalId})";
    }
}