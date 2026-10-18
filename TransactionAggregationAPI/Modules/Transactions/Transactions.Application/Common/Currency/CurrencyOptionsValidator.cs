using Microsoft.Extensions.Options;

namespace Modules.Transactions.Application.Common.Currency
{
    internal sealed class CurrencyOptionsValidator : IValidateOptions<CurrencyOptions>
    {
        public ValidateOptionsResult Validate(string? name, CurrencyOptions options) =>
            CurrencyOptions.Validate(options) is { } problem ? ValidateOptionsResult.Fail(problem) : ValidateOptionsResult.Success;
    }
}