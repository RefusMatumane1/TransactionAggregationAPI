using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Options;
using Modules.Transactions.Domain.Enums;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Modules.Transactions.Application.Services
{
    /// <summary>
    /// Rule-driven normalizer: every institution uses the default profile plus its own
    /// additions from <see cref="NormalizationOptions"/>, so onboarding a bank is a config
    /// change, not a new class. Profiles are resolved once, at construction, so a bad time
    /// zone or category name fails at startup rather than on the first delivery.
    /// </summary>
    public sealed partial class TransactionNormalizer : ITransactionNormalizer
    {
        private sealed record Profile(
            TimeZoneInfo TimeZone,
            IReadOnlyList<string> DescriptionPrefixes,
            IReadOnlyDictionary<string, TransactionCategory> CategoryMap);

        private readonly Profile _default;
        private readonly Dictionary<string, Profile> _institutions;

        public TransactionNormalizer(IOptions<NormalizationOptions> options)
        {
            var settings = options.Value;
            _default = BuildProfile("Default", settings.Default, parent: null);
            _institutions = settings.Institutions.ToDictionary(
                entry => entry.Key,
                entry => BuildProfile(entry.Key, entry.Value, _default),
                StringComparer.OrdinalIgnoreCase);
        }

        public NormalizedTransaction Normalize(ExternalTransactionDTO raw, string institution)
        {
            var profile = _institutions.GetValueOrDefault(institution) ?? _default;

            var rawDescription = raw.Description ?? string.Empty;
            var description = CleanDescription(rawDescription, profile);
            var rawCategory = string.IsNullOrWhiteSpace(raw.Category) ? null : raw.Category.Trim();

            return new NormalizedTransaction(
                ExternalId: raw.Id,
                Amount: raw.Amount,
                Currency: raw.Currency.Trim().ToUpperInvariant(),
                Description: description,
                DateUtc: ToUtc(raw.Date, profile.TimeZone),
                IsPending: BankTransactionStatus.IsPending(raw.Status),
                BankCategory: rawCategory is null ? null : MapCategory(rawCategory, profile),
                OriginalDescription: description == rawDescription ? null : rawDescription,
                OriginalCategory: rawCategory);
        }

        /// <summary>
        /// JSON gives Utc for a trailing "Z", Local for an explicit offset (already converted
        /// to this machine's zone, so ToUniversalTime recovers the exact instant), and
        /// Unspecified when there's no offset at all — that one is the bank's local time.
        /// GetUtcOffset (rather than ConvertTimeToUtc) never throws on a DST-gap time.
        /// </summary>
        private static DateTime ToUtc(DateTime date, TimeZoneInfo bankTimeZone) => date.Kind switch
        {
            DateTimeKind.Utc => date,
            DateTimeKind.Local => date.ToUniversalTime(),
            _ => new DateTimeOffset(date, bankTimeZone.GetUtcOffset(date)).UtcDateTime
        };

        private static string CleanDescription(string raw, Profile profile)
        {
            var cleaned = RepeatedWhitespace().Replace(raw.Trim(), " ");

            foreach (var prefix in profile.DescriptionPrefixes)
            {
                if (!StartsWithWholeWord(cleaned, prefix))
                    continue;

                var rest = cleaned[prefix.Length..].TrimStart(' ', ':', '-', '*');
                if (rest.Length > 0)
                    cleaned = rest;
                break;
            }

            return cleaned;
        }

        // "POS" must not strip the start of "POSTNET": the prefix has to end at a word boundary.
        private static bool StartsWithWholeWord(string text, string prefix) =>
            text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && (text.Length == prefix.Length || !char.IsLetterOrDigit(text[prefix.Length]));

        private static TransactionCategory? MapCategory(string bankLabel, Profile profile)
        {
            if (profile.CategoryMap.TryGetValue(bankLabel, out var mapped))
                return mapped;

            // A bank (or aggregator) that already uses our category names needs no mapping.
            return TryParseCategoryName(bankLabel, out var category) && category != TransactionCategory.Uncategorized
                ? category
                : null;
        }

        private static bool TryParseCategoryName(string name, out TransactionCategory category)
        {
            category = default;
            // Enum.TryParse also accepts numbers ("5"); a bank's numeric code is not our enum value.
            return !int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                   && Enum.TryParse(name, ignoreCase: true, out category)
                   && Enum.IsDefined(category);
        }

        private static Profile BuildProfile(string name, InstitutionNormalizationProfile settings, Profile? parent)
        {
            var timeZone = settings.TimeZone is null
                ? parent?.TimeZone ?? TimeZoneInfo.Utc
                : FindTimeZone(name, settings.TimeZone);

            // Longest first, so "POS PURCHASE" is tried before "POS".
            var prefixes = (parent?.DescriptionPrefixes ?? [])
                .Concat(settings.DescriptionPrefixes)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(p => p.Length)
                .ToList();

            var categoryMap = new Dictionary<string, TransactionCategory>(StringComparer.OrdinalIgnoreCase);
            foreach (var (label, category) in parent?.CategoryMap ?? new Dictionary<string, TransactionCategory>())
                categoryMap[label] = category;
            foreach (var (label, categoryName) in settings.CategoryMap)
            {
                if (!TryParseCategoryName(categoryName, out var category))
                    throw new InvalidOperationException(
                        $"Normalization profile '{name}' maps '{label}' to unknown category '{categoryName}'.");
                categoryMap[label.Trim()] = category;
            }

            return new Profile(timeZone, prefixes, categoryMap);
        }

        private static TimeZoneInfo FindTimeZone(string profileName, string id)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                throw new InvalidOperationException(
                    $"Normalization profile '{profileName}' has unknown time zone '{id}'.", ex);
            }
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex RepeatedWhitespace();
    }
}