using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Prometheus;

namespace TransactionAggregation.Hosting.Observability
{
    public static class MetricsEndpointExtensions
    {
        public const string MetricsPortKey = "Metrics:Port";
        public static WebApplication UsePrivateMetricsEndpoint(this WebApplication app)
        {
            var port = app.Configuration.GetValue<int?>(MetricsPortKey);

            if (port is null)
            {
                if (!app.Environment.IsDevelopment())
                    throw new InvalidOperationException(
                        $"{MetricsPortKey} must be configured outside Development so /metrics is not served on the public HTTP port.");

                app.UseMetricServer();
                return app;
            }

            var metricsPort = port.Value;
            app.UseWhen(context => context.Connection.LocalPort == metricsPort, branch => branch.UseMetricServer());
            return app;
        }
    }
}