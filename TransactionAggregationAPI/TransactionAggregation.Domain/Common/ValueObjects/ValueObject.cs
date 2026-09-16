namespace TransactionAggregation.Domain.Common.ValueObjects
{
    public abstract class ValueObject
    {
        protected abstract IEnumerable<object> GetEqualityComponents();

        public override bool Equals(object? obj)
        {
            if (obj == null || obj.GetType() != GetType())
                return false;

            var other = (ValueObject)obj;
            return GetEqualityComponents()
                .SequenceEqual(other.GetEqualityComponents());
        }

        public override int GetHashCode()
        {
            return GetEqualityComponents()
                .Select(x => x?.GetHashCode() ?? 0)
                .Aggregate((x, y) => x ^ y);
        }

        public bool Equals(ValueObject? other)
        {
            return Equals((object?)other);
        }

        // Without these, `==`/`!=` on two ValueObject-derived instances (e.g. two separately
        // constructed CustomerId wrapping the same Guid) fall back to reference equality —
        // which silently breaks any client-side (non-LINQ-translated) ownership/equality
        // check, such as `link.CustomerId != customerId` after the entity has already been
        // materialized. See RevokeBankLinkCommandHandler for the bug this caused.
        public static bool operator ==(ValueObject? left, ValueObject? right)
        {
            if (left is null && right is null)
                return true;

            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
    }
}
