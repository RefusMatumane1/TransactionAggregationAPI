using System.Globalization;
using TransactionAggregation.MockAggregator.Catalog;
using TransactionAggregation.MockAggregator.Feed;

namespace TransactionAggregation.MockAggregator.Banks;

/// <summary>
/// How one mock bank writes a transaction on the wire. These formats are INVENTED for the
/// mock — they are not the real banks' formats. Each exercises a different part of the
/// application's normalization stage; the matching rules live in the application's
/// normalization-rules.Development.json, never in the shipped rules file.
/// </summary>
public interface IBankStatementStyle
{
    string Institution { get; }

    DeliveryItem Render(GeneratedTransaction transaction);
}

internal static class SouthAfricanTime
{
    private static readonly TimeZoneInfo Sast = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");

    public static DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, Sast);

    /// <summary>"2026-09-10T12:00:00" — local time with no offset: the receiver must know the bank's zone.</summary>
    public static string WithoutOffset(DateTime utc) =>
        ToLocal(utc).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>"2026-09-10T12:00:00+02:00".</summary>
    public static string WithOffset(DateTime utc) =>
        new DateTimeOffset(ToLocal(utc), Sast.GetUtcOffset(utc)).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
}

internal static class StyleFormat
{
    public static string Utc(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    public static DeliveryItem Item(GeneratedTransaction t, string description, string date, string? category) =>
        new(t.Id, t.Amount, "ZAR", description, category, date, t.IsPending ? "pending" : "posted");
}

/// <summary>
/// Upper-case descriptions with "POS PURCHASE" boilerplate and padded spacing, local dates
/// with no offset, and its own category labels (e.g. "Takeaways", "Petrol").
/// </summary>
public sealed class FnbStyle : IBankStatementStyle
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["Groceries"] = "Grocery",
        ["Dining"] = "Takeaways",
        ["Transportation"] = "Petrol",
        ["Healthcare"] = "Medical",
        ["Utilities"] = "Municipal",
        ["Income"] = "Salary"
    };

    public string Institution => "FNB";

    public DeliveryItem Render(GeneratedTransaction t)
    {
        var name = t.Merchant.Name.ToUpperInvariant();
        var description = t.Merchant.Kind switch
        {
            SpendKind.CardPurchase => $"POS PURCHASE  {name}  {t.Merchant.City.ToUpperInvariant()}",
            SpendKind.DebitOrder => $"DEBIT ORDER  {name}",
            _ => name
        };

        return StyleFormat.Item(t, description, SouthAfricanTime.WithoutOffset(t.DateUtc),
            Labels.GetValueOrDefault(t.Merchant.Category));
    }
}

/// <summary>A bank-specific "ABSA CARD" prefix, dates with an explicit offset, no categories.</summary>
public sealed class AbsaStyle : IBankStatementStyle
{
    public string Institution => "Absa";

    public DeliveryItem Render(GeneratedTransaction t)
    {
        var description = t.Merchant.Kind switch
        {
            SpendKind.CardPurchase => $"ABSA CARD {t.Merchant.Name} {t.Merchant.City}",
            SpendKind.DebitOrder => $"DEBIT ORDER {t.Merchant.Name}",
            _ => t.Merchant.Name
        };

        return StyleFormat.Item(t, description, SouthAfricanTime.WithOffset(t.DateUtc), category: null);
    }
}

/// <summary>Clean merchant names, UTC dates, and categories already in the application's own names.</summary>
public sealed class CapitecStyle : IBankStatementStyle
{
    public string Institution => "Capitec";

    public DeliveryItem Render(GeneratedTransaction t)
    {
        var description = t.Merchant.Kind == SpendKind.DebitOrder
            ? $"DEBIT ORDER {t.Merchant.Name}"
            : t.Merchant.Kind == SpendKind.CardPurchase ? $"{t.Merchant.Name} {t.Merchant.City}" : t.Merchant.Name;

        return StyleFormat.Item(t, description, StyleFormat.Utc(t.DateUtc), t.Merchant.Category);
    }
}

/// <summary>
/// The "Other" case: a bank the application has no normalization profile for, so only the
/// default rules apply — its "PURCHASE" prefix is deliberately left for the default to miss.
/// </summary>
public sealed class StandardBankStyle : IBankStatementStyle
{
    public string Institution => "StandardBank";

    public DeliveryItem Render(GeneratedTransaction t)
    {
        var description = t.Merchant.Kind switch
        {
            SpendKind.CardPurchase => $"PURCHASE {t.Merchant.Name}",
            SpendKind.DebitOrder => $"DEBIT ORDER {t.Merchant.Name}",
            _ => t.Merchant.Name
        };

        return StyleFormat.Item(t, description, SouthAfricanTime.WithoutOffset(t.DateUtc), category: null);
    }
}

public static class BankStatementStyles
{
    public static readonly IReadOnlyDictionary<string, IBankStatementStyle> ByInstitution =
        new IBankStatementStyle[] { new FnbStyle(), new AbsaStyle(), new CapitecStyle(), new StandardBankStyle() }
            .ToDictionary(s => s.Institution, StringComparer.OrdinalIgnoreCase);
}