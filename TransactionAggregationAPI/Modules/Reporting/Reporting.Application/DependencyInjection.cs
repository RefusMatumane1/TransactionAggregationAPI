using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Modules.Reporting
{
    public static class ReportingApplicationDependencyInjection
    {
        public static IServiceCollection AddReportingApplication(this IServiceCollection services)
        {
            services.AddModuleApplication(typeof(AssemblyReference).Assembly);

            return services;
        }
    }

    internal sealed class AssemblyReference;
}
