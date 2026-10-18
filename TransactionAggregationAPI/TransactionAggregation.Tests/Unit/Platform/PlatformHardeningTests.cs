using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using System.Net;
using System.Reflection;
using System.Threading.RateLimiting;
using TransactionAggregation.Hosting;
using TransactionAggregation.Hosting.Observability;
using TransactionAggregationAPI.Authentication;
using TransactionAggregationAPI.RateLimiting;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Platform
{
    public class PlatformHardeningTests
    {
        private sealed class ManualClock : TimeProvider
        {
            private long _ticks = 1;
            public override long TimestampFrequency => TimeSpan.TicksPerSecond;
            public override long GetTimestamp() => _ticks;
            public void Advance(TimeSpan by) => _ticks += by.Ticks;
        }

        private static IConnectionMultiplexer UnreachableRedis()
        {
            var database = Substitute.For<IDatabase>();
            database.ScriptEvaluateAsync(Arg.Any<LuaScript>(), Arg.Any<object?>(), Arg.Any<CommandFlags>())
                .Returns<Task<RedisResult>>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));
            var redis = Substitute.For<IConnectionMultiplexer>();
            redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(database);
            return redis;
        }

        private static RedisFixedWindowRateLimiter Limiter(TimeProvider clock) =>
            new(UnreachableRedis(), "test", "client", new RedisRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) },
                NullLogger.Instance, clock);

        [Fact]
        public async Task RateLimiter_ReportsHowLongItHasBeenIdle_SoPartitionsCanBeEvicted()
        {
            var clock = new ManualClock();
            var limiter = Limiter(clock);

            clock.Advance(TimeSpan.FromSeconds(30));
            limiter.IdleDuration.Should().Be(TimeSpan.FromSeconds(30));

            (await limiter.AcquireAsync()).IsAcquired.Should().BeTrue("Redis being down still serves requests within the limit");
            limiter.IdleDuration.Should().Be(TimeSpan.Zero, "use resets the idle clock");
        }

        [Fact]
        public async Task RateLimiter_RedisDown_FallsBackToALocalLimit_SoAnOutageIsNotAnOpenDoor()
        {
            using var limiter = Limiter(new ManualClock());

            for (var i = 0; i < 10; i++)
                (await limiter.AcquireAsync()).IsAcquired.Should().BeTrue($"request {i + 1} is within the limit of 10");

            using var rejected = await limiter.AcquireAsync();

            rejected.IsAcquired.Should().BeFalse("the 11th request in the window is over the limit even though Redis is down");
            rejected.TryGetMetadata(MetadataName.RetryAfter, out _).Should().BeTrue();
        }

        private sealed class FakeThrottle(TimeSpan? blockedFor) : IAuthFailureThrottle
        {
            public int Recorded;
            public Task<TimeSpan?> BlockedForAsync(string client, CancellationToken cancellationToken) => Task.FromResult(blockedFor);
            public Task RecordFailureAsync(string client, CancellationToken cancellationToken)
            {
                Recorded++;
                return Task.CompletedTask;
            }
        }

        private static async Task<(DefaultHttpContext Context, bool Reached)> RunThrottleAsync(FakeThrottle throttle, int downstreamStatus)
        {
            var reached = false;
            var middleware = new TransactionAggregationAPI.Middleware.AuthFailureThrottleMiddleware(ctx =>
            {
                reached = true;
                ctx.Response.StatusCode = downstreamStatus;
                return Task.CompletedTask;
            }, throttle);
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

            await middleware.Invoke(context);
            return (context, reached);
        }

        [Fact]
        public async Task AuthFailureThrottle_ABlockedClient_IsTurnedAwayBeforeAuthentication()
        {
            var throttle = new FakeThrottle(TimeSpan.FromSeconds(42));

            var (context, reached) = await RunThrottleAsync(throttle, StatusCodes.Status200OK);

            reached.Should().BeFalse("no token is validated for a client that keeps failing");
            context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            context.Response.Headers.RetryAfter.ToString().Should().Be("42");
        }

        [Theory]
        [InlineData(StatusCodes.Status401Unauthorized, 1)]
        [InlineData(StatusCodes.Status200OK, 0)]
        [InlineData(StatusCodes.Status403Forbidden, 0)]
        public async Task AuthFailureThrottle_CountsOnlyFailedAuthentication(int downstreamStatus, int expectedRecorded)
        {
            var throttle = new FakeThrottle(null);

            var (_, reached) = await RunThrottleAsync(throttle, downstreamStatus);

            reached.Should().BeTrue();
            throttle.Recorded.Should().Be(expectedRecorded);
        }

        [Fact]
        public void RateLimiter_AttemptAcquire_NeverCallsRedis_SoTheRequestTakesTheAsyncPathAndIsCountedOnce()
        {
            var database = Substitute.For<IDatabase>();
            var redis = Substitute.For<IConnectionMultiplexer>();
            redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(database);
            var limiter = new RedisFixedWindowRateLimiter(redis, "test", "client",
                new RedisRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }, NullLogger.Instance);

            using var lease = limiter.AttemptAcquire();

            lease.IsAcquired.Should().BeFalse("the middleware then calls AcquireAsync, which does the one counted increment");
            lease.TryGetMetadata(MetadataName.RetryAfter, out _).Should().BeFalse("a deferral is not a rejection");
            database.ReceivedCalls().Should().BeEmpty("no blocking network call on the request thread");
        }

        [Fact]
        public async Task RateLimiter_IdlePartitions_AreEvicted_SoMemoryIsBoundedByActiveClients()
        {
            var partitioned = PartitionedRateLimiter.Create<string, string>(key =>
                RateLimitPartition.Get(key, _ => Limiter(TimeProvider.System)));

            for (var i = 0; i < 200; i++)
                partitioned.AttemptAcquire($"client-{i}").Dispose();
            PartitionCount(partitioned).Should().Be(200);

            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (PartitionCount(partitioned) > 0 && DateTime.UtcNow < deadline)
                await Task.Delay(500);

            PartitionCount(partitioned).Should().Be(0, "an idle client's limiter must not be kept for the life of the process");
        }

        private sealed class RejectedLease(TimeSpan? retryAfter) : RateLimitLease
        {
            public override bool IsAcquired => false;
            public override IEnumerable<string> MetadataNames => retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

            public override bool TryGetMetadata(string metadataName, out object? metadata)
            {
                metadata = metadataName == MetadataName.RetryAfter.Name ? retryAfter : null;
                return metadata is not null;
            }
        }

        [Theory]
        [InlineData(42_300, "43")]
        [InlineData(200, "1")]
        public void RateLimitRejection_TellsTheClientWhenToRetry_InWholeSecondsRoundedUp(int remainingMs, string expected)
        {
            var httpContext = new DefaultHttpContext();

            RateLimitingSetup.WriteRetryAfter(httpContext, new RejectedLease(TimeSpan.FromMilliseconds(remainingMs)));

            httpContext.Response.Headers.RetryAfter.ToString().Should().Be(expected);
        }

        [Fact]
        public void RateLimitRejection_WithoutRetryMetadata_SendsNoRetryAfter()
        {
            var httpContext = new DefaultHttpContext();

            RateLimitingSetup.WriteRetryAfter(httpContext, new RejectedLease(null));

            httpContext.Response.Headers.ContainsKey("Retry-After").Should().BeFalse();
        }

        [Fact]
        public async Task RateLimiter_RedisDown_AndFailingClosed_RetriesAfterAFullWindow()
        {
            var limiter = new RedisFixedWindowRateLimiter(UnreachableRedis(), "test", "client",
                new RedisRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), AllowRequestOnRedisFailure = false },
                NullLogger.Instance);

            using var lease = await limiter.AcquireAsync();

            lease.IsAcquired.Should().BeFalse();
            lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter).Should().BeTrue();
            retryAfter.Should().Be(TimeSpan.FromMinutes(1));
        }

        private static int PartitionCount(object partitionedLimiter)
        {
            var field = partitionedLimiter.GetType().GetField("_limiters", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var dictionary = field.GetValue(partitionedLimiter)!;
            return (int)dictionary.GetType().GetProperty("Count")!.GetValue(dictionary)!;
        }

        private sealed class CountingHandler(HttpStatusCode status) : HttpMessageHandler
        {
            public int Calls;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref Calls);
                return Task.FromResult(new HttpResponseMessage(status));
            }
        }

        private static (HttpClient Client, CountingHandler Handler) ResilientClient(HttpStatusCode status)
        {
            var handler = new CountingHandler(status);
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddServiceDiscovery();
            services.ConfigureResilientHttpClients();
            services.AddHttpClient("probe").ConfigurePrimaryHttpMessageHandler(() => handler);
            services.PostConfigureAll<HttpStandardResilienceOptions>(o =>
            {
                o.Retry.Delay = TimeSpan.FromMilliseconds(1);
                o.Retry.UseJitter = false;
            });

            var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("probe");
            client.BaseAddress = new Uri("http://provider.test");
            return (client, handler);
        }

        [Fact]
        public async Task OutboundHttp_RetriesIdempotentRequests()
        {
            var (client, handler) = ResilientClient(HttpStatusCode.ServiceUnavailable);

            await client.GetAsync("/accounts/me");

            handler.Calls.Should().Be(4, "a GET is safe to repeat: 1 attempt + 3 retries");
        }

        [Fact]
        public async Task OutboundHttp_NeverRetriesUnsafeRequests()
        {
            var (client, handler) = ResilientClient(HttpStatusCode.ServiceUnavailable);

            await client.PostAsync("/admin/realms/r/users", new StringContent("{}"));

            handler.Calls.Should().Be(1, "a POST that failed may already have taken effect (e.g. the user was created)");
        }

        [Fact]
        public void HealthChecks_OnlyRequiredDependenciesDecideReadiness()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:transactiondb"] = "Host=localhost;Database=unused",
                ["ConnectionStrings:redis"] = "localhost:6379,abortConnect=false",
                ["ConnectionStrings:seq"] = "http://seq.invalid"
            });
            builder.AddServiceDefaults();
            builder.AddApplicationModules();

            var registrations = builder.Services.BuildServiceProvider()
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value.Registrations;

            registrations.Select(r => r.Name).Should().BeEquivalentTo(["self", "redis", "postgres"],
                "Seq is a log sink: its outage must never pull pods out of rotation");
            registrations.Single(r => r.Name == "self").Tags.Should().Contain(Extensions.LiveTag);
            registrations.Single(r => r.Name == "postgres").Tags.Should().Contain(Extensions.ReadyTag);
            registrations.Single(r => r.Name == "postgres").FailureStatus.Should().Be(HealthStatus.Unhealthy);
            registrations.Single(r => r.Name == "redis").FailureStatus.Should().Be(HealthStatus.Degraded,
                "Redis is cache-only: degraded, still ready");
        }

        [Fact]
        public void MetricsEndpoint_OutsideDevelopment_RequiresAPrivatePort()
        {
            var app = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production }).Build();

            var act = () => app.UsePrivateMetricsEndpoint();

            act.Should().Throw<InvalidOperationException>().WithMessage("*Metrics:Port*");
        }

        [Fact]
        public void MissingDatabaseConnectionString_FailsStartup()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });

            var act = () => builder.AddApplicationModules();

            act.Should().Throw<InvalidOperationException>().WithMessage("*ConnectionStrings:transactiondb*");
        }

        [Fact]
        public void KeycloakOverPlainHttp_OutsideDevelopment_FailsStartup_InsteadOfFailingEveryRequest()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Authority"] = "http://keycloak:8080",
                ["Keycloak:Realm"] = "r",
                ["Keycloak:PublicIssuer"] = "https://id.example/realms/r",
                ["Keycloak:Audience"] = "a"
            });

            var act = () => builder.AddKeycloakAuthentication();

            act.Should().Throw<InvalidOperationException>().WithMessage("*https*");
        }
    }
}