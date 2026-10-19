using Microsoft.Extensions.Options;
using Modules.Currency.Application;
using Modules.Currency.Contracts;

namespace TransactionAggregation.Tests.Helpers
{
    public static class TestCurrencies
    {
        public static readonly ICurrencyCatalog Catalog = Create(("USD", 18m), ("EUR", 20m), ("GBP", 24m));

        public static ICurrencyCatalog Create(params (string Code, decimal Rate)[] rates) => Create("ZAR", rates);

        public static ICurrencyCatalog Create(string baseCurrency, params (string Code, decimal Rate)[] rates) =>
            new CurrencyCatalog(Options.Create(new CurrencyOptions
            {
                BaseCurrency = baseCurrency,
                Rates = rates.ToDictionary(r => r.Code, r => r.Rate, StringComparer.Ordinal)
            }));
    }
}