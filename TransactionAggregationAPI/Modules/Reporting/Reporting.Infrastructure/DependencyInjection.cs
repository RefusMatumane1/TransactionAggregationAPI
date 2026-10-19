using BuildingBlocks.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Modules.Reporting.Application.Aggregation;
using Modules.Reporting.Application.Persistence;
using Modules.Reporting.Infrastructure.Persistence;

namespace Modules.Reporting
{
    public static class ReportingInfrastructureDependencyInjection
    {
        public static IServiceCollection AddReportingModule(this IServiceCollection services)
        {
            services.AddReportingApplication();

            services.AddModuleDbContext<ReportingDbContext, IReportingDbContext>();
            services.AddScoped<IDailyTotalsRefresher, PostgresDailyTotalsRefresher>();

            return services;
        }
    }
}
