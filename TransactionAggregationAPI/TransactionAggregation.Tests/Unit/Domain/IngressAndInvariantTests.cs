using FluentAssertions;
using Microsoft.Extensions.Options;
using Modules.Audit.Domain;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Options;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Modules.Transactions.Application.Services;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Exceptions;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain
{
    public class IngressAndInvariantTests
    {
        private static readonly ReceiveBankTransactionsCommandValidator Validator = new();

        private static ReceiveBankTransactionsCommand Delivery(
            string id = "t-1", decimal amount = -10m, string description = "Card purchase", string externalAccountId = "ext-1",
            string? idempotencyKey = null) =>
            new("source", externalAccountId, null,
            [
                new ExternalTransactionDTO
                {
                    Id = id, Amount = amount, Currency = "ZAR", Description = description, Category = string.Empty, Date = DateTime.UtcNow
                }
            ], idempotencyKey);

        [Theory]
        [InlineData("CAFE\0X")]
        [InlineData("line\r\nbreak")]
        public void Validator_RejectsControlCharacters_InDescriptions(string description) =>
            Validator.Validate(Delivery(description: description)).Errors
                .Should().Contain(e => e.PropertyName == "Transactions[0].Description");

        [Fact]
        public void Validator_RejectsControlCharacters_InIdentifiers()
        {
            Validator.Validate(Delivery(id: "t\0")).IsValid.Should().BeFalse();
            Validator.Validate(Delivery(externalAccountId: "acc\0")).IsValid.Should().BeFalse();
            Validator.Validate(Delivery(idempotencyKey: "key\u0007")).IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validator_EnforcesTheColumnLengths()
        {
            Validator.Validate(Delivery(id: new string('x', 100))).IsValid.Should().BeTrue();
            Validator.Validate(Delivery(id: new string('x', 101))).IsValid.Should().BeFalse();
            Validator.Validate(Delivery(description: new string('d', 501))).IsValid.Should().BeFalse();
            Validator.Validate(Delivery(externalAccountId: new string('a', 201))).IsValid.Should().BeFalse();
        }

        [Theory]
        [InlineData("999999999999999.9999", true)]
        [InlineData("-999999999999999.9999", true)]
        [InlineData("1000000000000000", false)]
        [InlineData("10000000000000000", false)]
        public void Validator_BoundsTheAmountToTheNumericColumn(string amount, bool valid) =>
            Validator.Validate(Delivery(amount: decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture))).IsValid
                .Should().Be(valid);

        [Fact]
        public void Money_RefusesValuesTheColumnCannotStore()
        {
            var tooLarge = () => Money.Create(1_000_000_000_000_000m, "ZAR");
            var tooPrecise = () => Money.Create(1.00001m, "ZAR");

            tooLarge.Should().Throw<DomainException>();
            tooPrecise.Should().Throw<DomainException>();
            Money.Create(Money.MaxAbsoluteAmount, "zar").Currency.Should().Be("ZAR");
        }

        [Fact]
        public void Transaction_RefusesOverlongIdentifiersAndDescriptions()
        {
            var longId = () => TransactionSource.Create("FNB", new string('x', 101));
            var longDescription = () => TestTransactions.Create(-1m, new string('d', 501));
            var longAccount = () => TestTransactions.Create(-1m, account: new string('a', Transaction.MaxExternalAccountIdLength + 1));

            longId.Should().Throw<DomainException>();
            longDescription.Should().Throw<DomainException>();
            longAccount.Should().Throw<DomainException>();
        }

        [Fact]
        public void AuditEvent_RecordingARejection_CanNeverItselfBeRefused()
        {
            var audit = AuditEvent.Create(
                Guid.NewGuid(), "inbound.rejected", DateTime.UtcNow, "kafka",
                sourceName: new string('s', 500),
                externalAccountId: "acc\0" + new string('a', 500),
                idempotencyKey: new string('k', 500),
                externalTransactionId: new string('t', 500),
                detail: "bad\0" + new string('d', 5000),
                metadata: new Dictionary<string, string> { ["key\0"] = "value\0" },
                traceId: new string('0', 200));

            audit.SourceName.Should().HaveLength(AuditEvent.MaxIdentifierLength);
            audit.ExternalAccountId.Should().HaveLength(AuditEvent.MaxIdentifierLength).And.NotContain("\0");
            audit.IdempotencyKey.Should().HaveLength(AuditEvent.MaxIdentifierLength);
            audit.ExternalTransactionId.Should().HaveLength(AuditEvent.MaxExternalTransactionIdLength);
            audit.Detail.Should().HaveLength(AuditEvent.MaxDetailLength).And.NotContain("\0");
            audit.Metadata.Should().ContainKey("key").WhoseValue.Should().Be("value");
            audit.TraceId.Should().HaveLength(AuditEvent.MaxTraceIdLength);
        }

        private static TransactionCategorizationService Categorizer(Dictionary<string, string> keywords) =>
            new(Options.Create(new CategorizationOptions { Keywords = keywords }));

        private static TransactionCategory Categorize(TransactionCategorizationService service, string description, decimal amount = -10m) =>
            service.Categorize(description, amount, bankCategory: null);

        [Theory]
        [InlineData("CURRENT ACCOUNT TRANSFER")]
        [InlineData("Parents day gift")]
        [InlineData("Torrent subscription")]
        public void Categorizer_MatchesWholeWordsOnly(string description) =>
            Categorize(Categorizer(new() { ["rent"] = "Housing" }), description).Should().Be(TransactionCategory.Uncategorized);

        [Theory]
        [InlineData("RENT MAY 2026")]
        [InlineData("Monthly rent")]
        [InlineData("rent-payment")]
        public void Categorizer_StillMatchesTheWordItself(string description) =>
            Categorize(Categorizer(new() { ["rent"] = "Housing" }), description).Should().Be(TransactionCategory.Housing);

        [Fact]
        public void Categorizer_MostSpecificKeywordWins_RegardlessOfConfigurationOrder()
        {
            var generalFirst = Categorizer(new() { ["woolworths"] = "Shopping", ["woolworths food"] = "Groceries" });
            var specificFirst = Categorizer(new() { ["woolworths food"] = "Groceries", ["woolworths"] = "Shopping" });

            Categorize(generalFirst, "WOOLWORTHS  FOOD SANDTON").Should().Be(TransactionCategory.Groceries);
            Categorize(specificFirst, "WOOLWORTHS  FOOD SANDTON").Should().Be(TransactionCategory.Groceries);
            Categorize(generalFirst, "WOOLWORTHS SANDTON").Should().Be(TransactionCategory.Shopping);
        }
    }
}