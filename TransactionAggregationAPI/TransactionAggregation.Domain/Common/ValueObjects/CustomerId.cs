namespace TransactionAggregation.Domain.Common.ValueObjects
{
    public sealed class CustomerId : ValueObject
    {
        public Guid Value { get; }

        private CustomerId(Guid value)
        {
            Value = value;
        }

        public static CustomerId Create() => new(Guid.NewGuid());
        public static CustomerId CreateFrom(Guid value) => new(value);
        public static CustomerId CreateFrom(string value) => new(Guid.Parse(value));

        public override string ToString() => Value.ToString();

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}
