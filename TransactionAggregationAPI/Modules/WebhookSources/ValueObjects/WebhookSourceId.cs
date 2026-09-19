using SharedKernel.Common.ValueObjects;

namespace Modules.WebhookSources.ValueObjects
{
    public sealed class WebhookSourceId : ValueObject
    {
        public Guid Value { get; }

        private WebhookSourceId(Guid value) => Value = value;

        public static WebhookSourceId Create() => new(Guid.NewGuid());
        public static WebhookSourceId CreateFrom(Guid value) => new(value);

        public override string ToString() => Value.ToString();

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}
