using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Modules.Currency.Application;
using Modules.Currency.Contracts;

namespace Modules.Currency
{
    public static class CurrencyDependencyInjection
    {
        public static IServiceCollection AddCurrencyModule(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<CurrencyOptions>()
                .Bind(configuration.GetSection(CurrencyOptions.SectionName))
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<CurrencyOptions>, CurrencyOptionsValidator>();
            services.AddSingleton<ICurrencyCatalog, CurrencyCatalog>();

            return services;
        }
    }
}
