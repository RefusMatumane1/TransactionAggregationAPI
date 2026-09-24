using FluentAssertions;
using Modules.Transactions.Domain.Entities;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Persistence
{
    /// <summary>
    /// The Metadata ValueComparer runs on every SaveChanges and must tolerate null (e.g. a raw jsonb
    /// `null`): a throw there would abort an unrelated save. Pinned against the real EF Core model.
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