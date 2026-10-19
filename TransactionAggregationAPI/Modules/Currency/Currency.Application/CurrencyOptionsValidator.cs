using Microsoft.Extensions.Options;

namespace Modules.Currency.Application
{
    internal sealed class CurrencyOptionsValidator : IValidateOptions<CurrencyOptions>
    {
        public ValidateOptionsResult Validate(string? name, CurrencyOptions options) =>
            CurrencyOptions.Validate(options) is { } problem ? ValidateOptionsResult.Fail(problem) : ValidateOptionsResult.Success;
    }
}