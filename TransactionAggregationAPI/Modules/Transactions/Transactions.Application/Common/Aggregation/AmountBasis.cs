using Modules.Transactions.Application.Common.Currency;
using SharedKernel.Common.Models;

namespace Modules.Transactions.Application.Common.Aggregation
{
    internal sealed class AmountBasis
    {
        private AmountBasis(string currency, string? convertTo, ICurrencyCatalog catalog)
        {
            Currency = currency;
            ConvertTo = convertTo;
            Catalog = catalog;
        }

        public string Currency { get; }

        public string? ConvertTo { get; }

        public bool IsConverting => ConvertTo is not null;

        public ICurrencyCatalog Catalog { get; }

        public static Result<AmountBasis> Resolve(string? currency, string? convertTo, ICurrencyCatalog catalog)
        {
            if (convertTo is null)
                return Result.Success(new AmountBasis(currency ?? catalog.BaseCurrency, null, catalog));

            return catalog.IsSupported(convertTo)
                ? Result.Success(new AmountBasis(convertTo, convertTo, catalog))
                : Result.Failure<AmountBasis>(Error.Validation(
                    $"convertTo must be one of the supported currencies: {string.Join(", ", catalog.Supported)}."));
        }
    }

    internal sealed class TotalRow
    {
        public DateOnly Day { get; init; }
        public string SourceName { get; init; } = null!;
        public string ExternalAccountId { get; init; } = null!;
        public Domain.Enums.TransactionCategory Category { get; init; }
        public string Currency { get; init; } = null!;
        public decimal Income { get; init; }
        public decimal Expenses { get; init; }
        public int IncomeCount { get; init; }
        public int ExpenseCount { get; init; }
    }
}