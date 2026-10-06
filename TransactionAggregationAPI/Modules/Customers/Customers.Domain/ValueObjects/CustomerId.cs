using SharedKernel.Common.ValueObjects;

namespace Modules.Customers.Domain.ValueObjects
{
    public sealed class CustomerId : ValueObject, IComparable<CustomerId>, IComparable
    {
        public Guid Value { get; }

        private CustomerId(Guid value) => Value = value;

        public static CustomerId Create() => new(Guid.NewGuid());
        public static CustomerId CreateFrom(Guid value) => new(value);

        public override string ToString() => Value.ToString();

        public int CompareTo(CustomerId? other) => other is null ? 1 : Value.CompareTo(other.Value);

        public int CompareTo(object? obj) => CompareTo(obj as CustomerId);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}