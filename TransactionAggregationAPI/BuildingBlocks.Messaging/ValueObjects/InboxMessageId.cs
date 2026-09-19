using SharedKernel.Common.ValueObjects;

namespace BuildingBlocks.Messaging.ValueObjects
{
    public sealed class InboxMessageId : ValueObject
    {
        public Guid Value { get; }

        private InboxMessageId(Guid value) => Value = value;

        public static InboxMessageId Create() => new(Guid.NewGuid());
        public static InboxMessageId CreateFrom(Guid value) => new(value);

        public override string ToString() => Value.ToString();

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}
