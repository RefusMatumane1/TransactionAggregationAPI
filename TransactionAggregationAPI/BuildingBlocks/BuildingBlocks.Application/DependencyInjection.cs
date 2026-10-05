using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Application.Caching;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BuildingBlocks.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddModuleApplication(this IServiceCollection services, Assembly assembly)
        {
            services.AddValidatorsFromAssembly(assembly);
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
            return services;
        }

        public static IServiceCollection AddApplicationPipeline(this IServiceCollection services, CachingOptions? caching = null)
        {
            services.AddSingleton(caching ?? new CachingOptions());
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
            return services;
        }
    }
}