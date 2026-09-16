namespace TransactionAggregation.Domain.Common.ValueObjects
{
    public sealed class OutboxMessageId : ValueObject
    {
        public Guid Value { get; }

        private OutboxMessageId(Guid value) => Value = value;

        public static OutboxMessageId Create() => new(Guid.NewGuid());
        public static OutboxMessageId CreateFrom(Guid value) => new(value);

        public override string ToString() => Value.ToString();

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}