using FluentAssertions;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Persistence
{
    /// <summary>
    /// Transaction.Metadata's EF Core ValueComparer previously called
    /// c1.SequenceEqual(c2) directly. That's called on every SaveChangesAsync
    /// change-tracking pass, and neither the property's own IsRequired() (never
    /// set) nor its converter's read-side "?? new()" protects against a null
    /// reaching the comparer some other way (a raw SQL write of jsonb `null`,
    /// or any future EF Core materialization path that doesn't run the domain's
    /// object initializer first) — a null there would have thrown
    /// ArgumentNullException/NullReferenceException out of SaveChangesAsync and
    /// aborted an otherwise-unrelated save. These pin the fixed, null-tolerant
    /// comparer directly against the real EF Core model, not a hand-rolled copy.
    /// </summary>
    public class TransactionConfigurationTests
    {
        private static Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, string>> GetMetadataComparer()
        {
            using var context = InMemoryDbContextFactory.Create();
            var property = context.Model
                .FindEntityType(typeof(Transaction))!
                .FindProperty(nameof(Transaction.Metadata))!;

            return (Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, string>>)property.GetValueComparer();
        }

        [Fact]
        public void Equals_BothNull_ReturnsTrueWithoutThrowing()
        {
            var comparer = GetMetadataComparer();

            var act = () => comparer.Equals(null, null);

            act.Should().NotThrow();
            comparer.Equals(null, null).Should().BeTrue();
        }

        [Fact]
        public void Equals_OneNullOneEmpty_ReturnsTrueWithoutThrowing()
        {
            var comparer = GetMetadataComparer();

            var act = () => comparer.Equals(null, new Dictionary<string, string>());

            act.Should().NotThrow();
            comparer.Equals(null, new Dictionary<string, string>()).Should().BeTrue();
        }

        [Fact]
        public void Equals_NullVsNonEmpty_ReturnsFalseWithoutThrowing()
        {
            var comparer = GetMetadataComparer();

            var act = () => comparer.Equals(null, new Dictionary<string, string> { ["k"] = "v" });

            act.Should().NotThrow();
            comparer.Equals(null, new Dictionary<string, string> { ["k"] = "v" }).Should().BeFalse();
        }

        [Fact]
        public void GetHashCode_Null_ReturnsWithoutThrowing()
        {
            var comparer = GetMetadataComparer();

            var act = () => comparer.GetHashCode(null!);

            act.Should().NotThrow();
        }

        [Fact]
        public void Snapshot_Null_ReturnsEmptyDictionaryWithoutThrowing()
        {
            var comparer = GetMetadataComparer();

            var act = () => comparer.Snapshot(null!);

            act.Should().NotThrow();
            comparer.Snapshot(null!).Should().NotBeNull().And.BeEmpty();
        }
    }
}
