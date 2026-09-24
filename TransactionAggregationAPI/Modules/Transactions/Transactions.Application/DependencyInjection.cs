using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Options;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions;
using Modules.Transactions.Application.Mappings;
using Modules.Transactions.Application.Services;
using SharedKernel.Common.Behaviors;
using SharedKernel.Common.Interfaces;
using System.Reflection;

namespace Modules.Transactions
{
    public static class TransactionsApplicationDependencyInjection
    {
        public static IServiceCollection AddTransactionsApplication(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<CategorizationOptions>(
                configuration.GetSection(CategorizationOptions.SectionName));
            services.AddScoped<ITransactionCategorizationService, TransactionCategorizationService>();

            services.Configure<NormalizationOptions>(
                configuration.GetSection(NormalizationOptions.SectionName));
            services.AddSingleton<ITransactionNormalizer, TransactionNormalizer>();
            services.AddScoped<IAnalyticsService, AnalyticsService>();

            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(GetTransactionsQueryHandler).Assembly);
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
            });

            var config = TypeAdapterConfig.GlobalSettings;
            config.Scan(Assembly.GetExecutingAssembly());
            services.AddSingleton(config);

            config.Scan(typeof(TransactionProfile).Assembly);
            services.AddScoped<IMapper, ServiceMapper>();

            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            return services;
        }
    }
}