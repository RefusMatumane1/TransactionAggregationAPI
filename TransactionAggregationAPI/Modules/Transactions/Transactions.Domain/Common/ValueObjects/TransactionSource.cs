using SharedKernel.Common.ValueObjects;
using SharedKernel.Exceptions;

namespace Modules.Transactions.Domain.Common.ValueObjects
{
    public sealed class TransactionSource : ValueObject
    {
        public string Name { get; }
        public string ExternalId { get; }

        private TransactionSource(string name, string externalId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Transaction source name cannot be empty");

            if (string.IsNullOrWhiteSpace(externalId))
                throw new DomainException("External ID cannot be empty");

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