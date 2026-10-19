using Microsoft.Extensions.Options;
using Modules.Currency.Contracts;
using SharedKernel.Common.ValueObjects;
using System.Security.Cryptography;
using System.Text;

namespace Modules.Currency.Application
{
    internal sealed class CurrencyCatalog : ICurrencyCatalog
    {
        private readonly Dictionary<string, decimal> _toBase;

        public CurrencyCatalog(IOptions<CurrencyOptions> options)
        {
            var configured = options.Value;
            if (CurrencyOptions.Validate(configured) is { } problem)
                throw new InvalidOperationException(problem);

            BaseCurrency = configured.BaseCurrency;
            _toBase = configured.Rates.Where(r => r.Value > 0).ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);
            _toBase[BaseCurrency] = 1m;
            Supported = [.. _toBase.Keys.Order(StringComparer.Ordinal)];

            var table = string.Join(';', Supported.Select(code => string.Create(
                System.Globalization.CultureInfo.InvariantCulture, $"{code}={_toBase[code]}")));
            Version = System.Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{BaseCurrency}|{table}")))[..12];
        }

        public string BaseCurrency { get; }

        public IReadOnlyList<string> Supported { get; }

        public string Version { get; }

        public bool IsSupported(string? currency) =>
            currency is not null && _toBase.ContainsKey(CurrencyCode.Normalize(currency));

        public decimal? Rate(string from, string to) =>
            _toBase.TryGetValue(from, out var fromRate) && _toBase.TryGetValue(to, out var toRate)
                ? fromRate / toRate
                : null;

        public decimal? Convert(decimal amount, string from, string to) =>
            Rate(from, to) is { } rate
                ? decimal.Round(amount * rate, MoneyScale.Max, MidpointRounding.AwayFromZero)
                : null;
    }
}