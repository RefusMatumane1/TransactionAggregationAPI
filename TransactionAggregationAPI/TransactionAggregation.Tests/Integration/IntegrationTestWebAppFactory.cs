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
using TransactionAggregation.Persistence;

namespace TransactionAggregation.Tests.Integration
{
    /// <summary>
    /// Creates an in-process test server with an in-memory database.
    /// Replaces PostgreSQL, Redis, Seq and background workers so tests run
    /// without any external infrastructure.
    /// </summary>
    public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Use Development so that app.MapDefaultEndpoints() registers /health and /alive.
            // All external dependencies (PostgreSQL, Redis, Seq, rate limiter) are replaced
            // below, so "Development" mode has no side-effects here.
            builder.UseEnvironment("Development");

            // UseSetting writes directly into the host configuration dictionary before
            // WebApplication.CreateBuilder processes services.  Aspire extensions such as
            // AddSeqEndpoint and AddRedisClient validate these connection strings at
            // registration time, so they must be present at that point.
            builder.UseSetting("ConnectionStrings:transactiondb", "Host=localhost;Database=test");
            builder.UseSetting("ConnectionStrings:redis",         "localhost:6379");
            builder.UseSetting("ConnectionStrings:seq",           "http://localhost:5341");

            // Instruct Aspire components to skip health-check registrations that need
            // real infrastructure.  Without this, AddSeqEndpoint throws when it cannot
            // resolve a live Seq server URL.
            builder.UseSetting("Aspire:Seq:DisableHealthChecks",   "true");
            builder.UseSetting("Aspire:Redis:DisableHealthChecks", "true");

            builder.ConfigureServices(services =>
            {
                // ── DbContext: replace Npgsql with InMemory ──────────────────────────
                //
                // Program.cs calls builder.EnrichNpgsqlDbContext<ApplicationDbContext>()
                // which registers IConfigureOptions<DbContextOptions<ApplicationDbContext>>
                // for the Npgsql provider.  Simply removing DbContextOptions<T> is not
                // enough; if those option-configurators remain, EF Core will apply them
                // on the first request and find two database providers (Npgsql + InMemory),
                // causing an InvalidOperationException.  We must remove them too.

                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();

                // Aspire's EnrichNpgsqlDbContext registers provider callbacks through
                // two different EF Core extension points.  Both must be cleared so that
                // EF Core's internal service provider does not see two database providers
                // (Npgsql + InMemory) and throw InvalidOperationException at startup.
                //
                //  • IConfigureOptions<DbContextOptions<T>>  – classic options pipeline
                //  • IDbContextOptionsConfiguration<T>       – EF Core 8+ pipeline used
                //                                              by Aspire 13.x enrichment
                var toRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(IConfigureOptions<DbContextOptions<ApplicationDbContext>>) ||
                        d.ServiceType == typeof(IDbContextOptionsConfiguration<ApplicationDbContext>))
                    .ToList();
                foreach (var d in toRemove)
                    services.Remove(d);

                // Swap in an isolated in-memory database per test run so tests are
                // independent even when the factory is shared across a test class.
                var dbName = $"TestDb_{Guid.NewGuid()}";
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                // ── Background services ──────────────────────────────────────────────
                // Remove hosted services (background sync workers, etc.) so they don't
                // try to connect to real infrastructure during tests.
                services.RemoveAll<IHostedService>();

                // ── Distributed cache ────────────────────────────────────────────────
                // Replace the Redis distributed cache with an in-process memory cache.
                services.RemoveAll<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                services.AddDistributedMemoryCache();

                // ── Health checks ────────────────────────────────────────────────────
                // Aspire's EnrichNpgsqlDbContext and AddRedisClient register health checks
                // that open real connections.  Clear them all and add only the "self" liveness
                // check so /health returns 200 and /alive also returns 200 in tests.
                services.Configure<HealthCheckServiceOptions>(opts => opts.Registrations.Clear());
                services.AddHealthChecks()
                    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

                // ── Rate limiter ──────────────────────────────────────────────────────
                // Customer endpoints use .RequireRateLimiting("FixedWindow") with a limit
                // of 10 req/min.  All integration tests share the same test server
                // (IClassFixture), so the combined request count would exceed the limit
                // and produce 503 responses.  We remove the existing IConfigureOptions
                // registration and replace it with an effectively unlimited policy so
                // that the same policy name ("FixedWindow") is still resolvable.
                services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
                services.Configure<RateLimiterOptions>(options =>
                {
                    options.AddFixedWindowLimiter("FixedWindow", opt =>
                    {
                        opt.PermitLimit = int.MaxValue;
                        opt.Window      = TimeSpan.FromMinutes(1);
                        opt.QueueLimit  = int.MaxValue;
                    });
                    options.GlobalLimiter = null;
                });
            });
        }
    }
}
