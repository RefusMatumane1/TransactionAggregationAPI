using BuildingBlocks.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.Currency;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Options;
using Modules.Transactions.Application.Services;
using System.Reflection;

namespace Modules.Transactions
{
    public static class TransactionsApplicationDependencyInjection
    {
        public static IServiceCollection AddTransactionsApplication(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<CategorizationOptions>(
                configuration.GetSection(CategorizationOptions.SectionName));
            services.AddSingleton<ITransactionCategorizationService, TransactionCategorizationService>();

            services.Configure<NormalizationOptions>(
                configuration.GetSection(NormalizationOptions.SectionName));
            services.AddSingleton<ITransactionNormalizer, TransactionNormalizer>();

            services.AddOptions<IngestionOptions>()
                .Bind(configuration.GetSection(IngestionOptions.SectionName))
                .Validate(IngestionOptions.IsValid,
                    $"Ingestion:MaxTransactionsPerDelivery must be between 1 and {IngestionOptions.MaxAllowedTransactionsPerDelivery}.")
                .ValidateOnStart();

            services.AddOptions<CurrencyOptions>()
                .Bind(configuration.GetSection(CurrencyOptions.SectionName))
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<CurrencyOptions>, CurrencyOptionsValidator>();
            services.AddSingleton<ICurrencyCatalog, CurrencyCatalog>();

            services.AddModuleApplication(Assembly.GetExecutingAssembly());

            return services;
        }
    }
}