using FluentAssertions;
using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Options;
using Modules.Transactions.Application.Services;
using Modules.Transactions.Domain.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Services
{
    public class TransactionNormalizerTests
    {
        private const string Sast = "Africa/Johannesburg";

        private static TransactionNormalizer Build(
            InstitutionNormalizationProfile? defaults = null,
            Dictionary<string, InstitutionNormalizationProfile>? institutions = null) =>
            new(Options.Create(new NormalizationOptions
            {
                Default = defaults ?? new InstitutionNormalizationProfile { TimeZone = Sast },
                Institutions = institutions ?? new()
            }));

        private static ExternalTransactionDTO Raw(
            string description = "Checkers Sandton",
            DateTime? date = null,
            string? category = null,
            string currency = "ZAR",
            string? status = null,
            string id = "ext-1") => new()
            {
                Id = id,
                Amount = -99.95m,
                Currency = currency,
                Description = description,
                Category = category!,
                Date = date ?? new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc),
                Status = status
            };

        [Fact]
        public void Normalize_DateWithoutOffset_IsReadAsTheBanksLocalTime()
        {
            var local = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Unspecified);

            var result = Build().Normalize(Raw(date: local), "FNB");

            result.DateUtc.Should().Be(new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc));
            result.DateUtc.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public void Normalize_DateWithAnOffset_KeepsTheSameInstant()
        {
            var fromOffset = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.FromHours(2)).LocalDateTime;

            var result = Build().Normalize(Raw(date: fromOffset), "FNB");

            result.DateUtc.Should().Be(new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc));
            result.DateUtc.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public void Normalize_UtcDate_IsUnchanged()
        {
            var utc = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

            Build().Normalize(Raw(date: utc), "FNB").DateUtc.Should().Be(utc);
        }

        [Fact]
        public void Normalize_InstitutionTimeZone_OverridesTheDefault()
        {
            var normalizer = Build(institutions: new() { ["OffshoreBank"] = new() { TimeZone = "UTC" } });
            var local = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Unspecified);

            normalizer.Normalize(Raw(date: local), "OffshoreBank").DateUtc.Hour.Should().Be(12);
            normalizer.Normalize(Raw(date: local), "FNB").DateUtc.Hour.Should().Be(10, "FNB has no profile, so the default (SAST) applies");
        }

        [Fact]
        public void Normalize_InstitutionName_IsMatchedCaseInsensitively()
        {
            var normalizer = Build(institutions: new() { ["Capitec"] = new() { DescriptionPrefixes = ["CAPITEC PAY"] } });

            normalizer.Normalize(Raw("CAPITEC PAY Netflix"), "capitec").Description.Should().Be("Netflix");
        }

        [Fact]
        public void Normalize_Description_IsTrimmedAndWhitespaceCollapsed()
        {
            var result = Build().Normalize(Raw("  Checkers   Sandton \t City  "), "FNB");

            result.Description.Should().Be("Checkers Sandton City");
            result.OriginalDescription.Should().Be("  Checkers   Sandton \t City  ");
        }

        [Theory]
        [InlineData("POS PURCHASE Woolworths Rosebank", "Woolworths Rosebank")]
        [InlineData("pos purchase: Woolworths Rosebank", "Woolworths Rosebank")]
        [InlineData("DEBIT ORDER - Discovery Health", "Discovery Health")]
        public void Normalize_KnownPrefix_IsStripped(string raw, string expected)
        {
            var normalizer = Build(new() { TimeZone = Sast, DescriptionPrefixes = ["POS PURCHASE", "DEBIT ORDER"] });

            normalizer.Normalize(Raw(raw), "FNB").Description.Should().Be(expected);
        }

        [Fact]
        public void Normalize_PrefixThatIsOnlyPartOfAWord_IsNotStripped()
        {
            var normalizer = Build(new() { TimeZone = Sast, DescriptionPrefixes = ["POS"] });

            normalizer.Normalize(Raw("POSTNET Parcel"), "FNB").Description.Should().Be("POSTNET Parcel");
        }

        [Fact]
        public void Normalize_DescriptionThatIsOnlyAPrefix_IsKeptRatherThanEmptied()
        {
            var normalizer = Build(new() { TimeZone = Sast, DescriptionPrefixes = ["DEBIT ORDER"] });

            normalizer.Normalize(Raw("DEBIT ORDER"), "FNB").Description.Should().Be("DEBIT ORDER");
        }

        [Fact]
        public void Normalize_LongestPrefixWins()
        {
            var normalizer = Build(new() { TimeZone = Sast, DescriptionPrefixes = ["POS", "POS PURCHASE"] });

            normalizer.Normalize(Raw("POS PURCHASE Spar"), "FNB").Description.Should().Be("Spar");
        }

        [Fact]
        public void Normalize_InstitutionPrefixes_AddToTheDefaultOnes()
        {
            var normalizer = Build(
                new() { TimeZone = Sast, DescriptionPrefixes = ["POS PURCHASE"] },
                new() { ["Absa"] = new() { DescriptionPrefixes = ["ABSA CARD"] } });

            normalizer.Normalize(Raw("ABSA CARD Engen"), "Absa").Description.Should().Be("Engen");
            normalizer.Normalize(Raw("POS PURCHASE Engen"), "Absa").Description.Should().Be("Engen");
            normalizer.Normalize(Raw("ABSA CARD Engen"), "FNB").Description.Should().Be("ABSA CARD Engen");
        }

        [Fact]
        public void Normalize_CleanDescription_RecordsNoOriginal()
        {
            Build().Normalize(Raw("Checkers Sandton"), "FNB").OriginalDescription.Should().BeNull();
        }

        [Fact]
        public void Normalize_MappedBankCategory_IsTranslated()
        {
            var normalizer = Build(new() { TimeZone = Sast, CategoryMap = new() { ["Food & Drink"] = "Dining" } });

            var result = normalizer.Normalize(Raw(category: "food & drink"), "FNB");

            result.BankCategory.Should().Be(TransactionCategory.Dining);
            result.OriginalCategory.Should().Be("food & drink");
        }

        [Fact]
        public void Normalize_InstitutionCategoryMap_OverridesTheDefaultEntry()
        {
            var normalizer = Build(
                new() { TimeZone = Sast, CategoryMap = new() { ["Transfer"] = "Transfer" } },
                new() { ["Capitec"] = new() { CategoryMap = new() { ["Transfer"] = "Income" } } });

            normalizer.Normalize(Raw(category: "Transfer"), "Capitec").BankCategory.Should().Be(TransactionCategory.Income);
            normalizer.Normalize(Raw(category: "Transfer"), "FNB").BankCategory.Should().Be(TransactionCategory.Transfer);
        }

        [Fact]
        public void Normalize_BankCategoryUsingOurName_NeedsNoMapping()
        {
            Build().Normalize(Raw(category: "groceries"), "FNB").BankCategory.Should().Be(TransactionCategory.Groceries);
        }

        [Theory]
        [InlineData("5")]
        [InlineData("Uncategorized")]
        [InlineData("Lifestyle")]
        public void Normalize_UnusableBankCategory_GivesNoHintButKeepsTheOriginal(string label)
        {
            var result = Build().Normalize(Raw(category: label), "FNB");

            result.BankCategory.Should().BeNull();
            result.OriginalCategory.Should().Be(label);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Normalize_NoBankCategory_GivesNoHintAndNoOriginal(string? category)
        {
            var result = Build().Normalize(Raw(category: category), "FNB");

            result.BankCategory.Should().BeNull();
            result.OriginalCategory.Should().BeNull();
        }

        [Fact]
        public void Normalize_Currency_IsTrimmedAndUpperCased()
        {
            Build().Normalize(Raw(currency: " zar"), "FNB").Currency.Should().Be("ZAR");
        }

        [Fact]
        public void Normalize_ExternalId_IsNeverChanged()
        {
            Build().Normalize(Raw(id: " ext-1 "), "FNB").ExternalId.Should().Be(" ext-1 ");
        }

        [Theory]
        [InlineData("pending", true)]
        [InlineData("PENDING", true)]
        [InlineData("posted", false)]
        [InlineData(null, false)]
        public void Normalize_Status_BecomesIsPending(string? status, bool expected)
        {
            Build().Normalize(Raw(status: status), "FNB").IsPending.Should().Be(expected);
        }

        [Fact]
        public void Constructor_UnknownTimeZone_FailsFast()
        {
            var act = () => Build(institutions: new() { ["FNB"] = new() { TimeZone = "Mars/Olympus_Mons" } });

            act.Should().Throw<InvalidOperationException>().WithMessage("*FNB*Mars/Olympus_Mons*");
        }

        [Fact]
        public void Constructor_CategoryMapToUnknownCategory_FailsFast()
        {
            var act = () => Build(new() { TimeZone = Sast, CategoryMap = new() { ["Fuel"] = "Petrol" } });

            act.Should().Throw<InvalidOperationException>().WithMessage("*Fuel*Petrol*");
        }

        [Fact]
        public void ShippedRules_AreValidAndReadDatesAsSouthAfricanTime()
        {
            var local = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Unspecified);

            var result = TestNormalizers.Shipped().Normalize(Raw("POS PURCHASE Spar", local, "Grocery"), "Capitec");

            result.DateUtc.Should().Be(new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc));
            result.Description.Should().Be("Spar");
            result.BankCategory.Should().Be(TransactionCategory.Groceries);
        }

        [Theory]
        [InlineData("StandardBank", "PURCHASE Woolworths", "Woolworths")]
        [InlineData("StandardBank", "purchase: Woolworths", "Woolworths")]
        [InlineData("StandardBank", "POS PURCHASE Woolworths", "Woolworths")]
        [InlineData("StandardBank", "PURCHASE", "PURCHASE")]
        [InlineData("StandardBank", "PURCHASES R US", "PURCHASES R US")]
        [InlineData("StandardBank", "SBSA CARD Engen", "Engen")]
        [InlineData("StandardBank", "DEBIT ORDER Discovery", "Discovery")]
        [InlineData("Capitec", "CAPITEC PAY Takealot", "Takealot")]
        [InlineData("Capitec", "DEBIT ORDER Vodacom", "Vodacom")]
        [InlineData("Capitec", "Nandos Fourways", "Nandos Fourways")]
        [InlineData("Capitec", "PURCHASE Nandos", "PURCHASE Nandos")]
        [InlineData("capitec", "  CONTACTLESS   PURCHASE   Spar ", "Spar")]
        [InlineData("Other", "PURCHASE Spar", "PURCHASE Spar")]
        public void ShippedRules_Descriptions(string institution, string raw, string expected)
        {
            TestNormalizers.Shipped().Normalize(Raw(raw), institution).Description.Should().Be(expected);
        }

        [Theory]
        [InlineData("StandardBank", "Food & Groceries", TransactionCategory.Groceries)]
        [InlineData("StandardBank", "cellphone", TransactionCategory.Utilities)]
        [InlineData("Capitec", "Eating Out", TransactionCategory.Dining)]
        [InlineData("Capitec", "Groceries", TransactionCategory.Groceries)]
        [InlineData("Capitec", "Fuel", TransactionCategory.Transportation)]
        [InlineData("Other", "Eating Out", null)]
        [InlineData("Capitec", "Mystery", null)]
        public void ShippedRules_Categories(string institution, string label, TransactionCategory? expected)
        {
            TestNormalizers.Shipped().Normalize(Raw(category: label), institution).BankCategory.Should().Be(expected);
        }

        [Theory]
        [InlineData("Capitec", "Online Stores", TransactionCategory.Shopping)]
        [InlineData("StandardBank", "Takeaways", TransactionCategory.Dining)]
        [InlineData("FNB", "Takeaways", TransactionCategory.Dining)]
        [InlineData("StandardBank", "Eating Out", null)]
        public void DevelopmentRules_Categories(string institution, string label, TransactionCategory? expected)
        {
            TestNormalizers.ShippedFor("Development").Normalize(Raw(category: label), institution)
                .BankCategory.Should().Be(expected);
        }

        [Fact]
        public void DevelopmentRules_KeepProductionPrefixesForCapitecAndStandardBank()
        {
            var normalizer = TestNormalizers.ShippedFor("Development");

            normalizer.Normalize(Raw("PURCHASE Spar"), "StandardBank").Description.Should().Be("Spar");
            normalizer.Normalize(Raw("CAPITEC PAY Spar"), "Capitec").Description.Should().Be("Spar");
        }

        [Fact]
        public void ShippedRules_CapitecUtcAndStandardBankLocalDates_ResolveToTheSameInstant()
        {
            var normalizer = TestNormalizers.Shipped();
            var utc = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);
            var local = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Unspecified);

            normalizer.Normalize(Raw(date: utc), "Capitec").DateUtc.Should().Be(utc);
            normalizer.Normalize(Raw(date: local), "StandardBank").DateUtc.Should().Be(utc);
        }
    }
}