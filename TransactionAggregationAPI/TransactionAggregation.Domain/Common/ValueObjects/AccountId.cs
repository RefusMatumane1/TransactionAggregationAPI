namespace TransactionAggregation.Domain.Common.ValueObjects
{
    public sealed class AccountId : ValueObject
    {
        public Guid Value { get; }

        private AccountId(Guid value) => Value = value;

        public static AccountId Create() => new(Guid.NewGuid());
        public static AccountId CreateFrom(Guid value) => new(value);

        public override string ToString() => Value.ToString();

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}
