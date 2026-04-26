using System;
using System.Collections.Generic;
using System.Text;

namespace TransactionAggregation.Domain.Common.ValueObjects
{
    public sealed class TransactionId : ValueObject
    {
        public Guid Value { get; }

        private TransactionId(Guid value)
        {
            Value = value;
        }

        public static TransactionId Create() => new(Guid.NewGuid());
        public static TransactionId CreateFrom(Guid value) => new(value);
        public static TransactionId CreateFrom(string value) => new(Guid.Parse(value));

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}
