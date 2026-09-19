using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using BuildingBlocks.Messaging.Persistence;
using Modules.WebhookSources.Persistence;
using SharedKernel.Abstractions.Authentication;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Persistence;
using TransactionAggregation.Tests.Helpers;

namespace TransactionAggregation.Tests.Integration
{
    public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {

            builder.UseEnvironment("Development");

            builder.UseSetting("ConnectionStrings:transactiondb", "Host=localhost;Database=test");
            builder.UseSetting("ConnectionStrings:redis", "localhost:6379");
            builder.UseSetting("ConnectionStrings:seq", "http://localhost:5341");

            builder.UseSetting("Aspire:Seq:DisableHealthChecks", "true");
            builder.UseSetting("Aspire:Redis:DisableHealthChecks", "true");

            builder.ConfigureServices(services =>
            {
                ReplaceWithInMemory<ApplicationDbContext>(services);
                ReplaceWithInMemory<MessagingDbContext>(services);
                ReplaceWithInMemory<WebhookSourcesDbContext>(services);

                services.RemoveAll<IHostedService>();

                services.RemoveAll<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                services.AddDistributedMemoryCache();

                services.Configure<HealthCheckServiceOptions>(opts => opts.Registrations.Clear());
                services.AddHealthChecks()
                    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

                services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
                services.Configure<RateLimiterOptions>(options =>
                {
                    options.AddFixedWindowLimiter("FixedWindow", opt =>
                    {
                        opt.PermitLimit = int.MaxValue;
                        opt.Window = TimeSpan.FromMinutes(1);
                        opt.QueueLimit = int.MaxValue;
                    });
                    options.GlobalLimiter = null;
                });

                services.AddAuthentication(options =>
                                {
                                    options.DefaultScheme = TestAuthHandler.SchemeName;
                                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                services.RemoveAll<IKeycloakAdminClient>();
                services.AddSingleton<IKeycloakAdminClient, FakeKeycloakAdminClient>();
            });
        }

        /// <summary>
        /// Program.cs registers ApplicationDbContext/MessagingDbContext against a
        /// shared NpgsqlConnection (see docs/adr/0009-schema-per-module-database-strategy.md)
        /// and WebhookSourcesDbContext against its own Npgsql connection string —
        /// none reachable in this test host. Swaps each for its own isolated
        /// in-memory database, same as the single-context swap this replaced.
        /// </summary>
        private static void ReplaceWithInMemory<TContext>(IServiceCollection services) where TContext : DbContext
        {
            services.RemoveAll<DbContextOptions<TContext>>();
            services.RemoveAll<TContext>();

            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(IConfigureOptions<DbContextOptions<TContext>>) ||
                    d.ServiceType == typeof(IDbContextOptionsConfiguration<TContext>))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            var dbName = $"TestDb_{typeof(TContext).Name}_{Guid.NewGuid()}";
            services.AddDbContext<TContext>(options => options.UseInMemoryDatabase(dbName));
        }
    }
}
