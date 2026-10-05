using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Domain.Common.ValueObjects
{
    public sealed class TransactionId : ValueObject, IComparable<TransactionId>, IComparable
    {
        public Guid Value { get; }

        private TransactionId(Guid value)
        {
            Value = value;
        }

        public static TransactionId Create() => new(Guid.NewGuid());
        public static TransactionId CreateFrom(Guid value) => new(value);
        public static TransactionId CreateFrom(string value) => new(Guid.Parse(value));

        public int CompareTo(TransactionId? other) => other is null ? 1 : Value.CompareTo(other.Value);

        public int CompareTo(object? obj) => CompareTo(obj as TransactionId);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}