using SharedKernel.Common.ValueObjects;

namespace TransactionAggregation.Domain.Common.ValueObjects
{
    public sealed class BankLinkId : ValueObject
    {
        public Guid Value { get; }

        private BankLinkId(Guid value) => Value = value;

        public static BankLinkId Create() => new(Guid.NewGuid());
        public static BankLinkId CreateFrom(Guid value) => new(value);

        public override string ToString() => Value.ToString();

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}