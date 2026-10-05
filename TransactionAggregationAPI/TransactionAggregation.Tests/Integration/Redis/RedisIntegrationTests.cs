using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using System.Threading.RateLimiting;
using TransactionAggregation.Hosting.Caching;
using TransactionAggregationAPI.RateLimiting;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Redis
{
    public sealed class RedisContainerFixture : IAsyncLifetime
    {
        private IContainer _container = null!;

        public IConnectionMultiplexer Redis { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            _container = new ContainerBuilder("redis:8.6")
                .WithPortBinding(6379, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
                .Build();
            await _container.StartAsync();

            Redis = await ConnectionMultiplexer.ConnectAsync($"{_container.Hostname}:{_container.GetMappedPublicPort(6379)}");
        }

        public async Task DisposeAsync()
        {
            Redis?.Dispose();
            if (_container is not null)
                await _container.DisposeAsync();
        }
    }

    public class RedisIntegrationTests : IClassFixture<RedisContainerFixture>
    {
        private readonly RedisContainerFixture _fixture;

        public RedisIntegrationTests(RedisContainerFixture fixture)
        {
            _fixture = fixture;
        }

        private RedisCacheService Cache() => new(_fixture.Redis, NullLogger<RedisCacheService>.Instance);

        [Fact]
        public async Task ScopeVersion_StartsAtZero_AndInvalidationBumpsItWithABoundedTtl()
        {
            var cache = Cache();
            var scope = $"transactions:{Guid.NewGuid()}";

            (await cache.GetScopeVersionAsync(scope)).Should().Be(0);
            await cache.InvalidateScopeAsync(scope);
            await cache.InvalidateScopeAsync(scope);

            (await cache.GetScopeVersionAsync(scope)).Should().Be(2);
            var ttl = await _fixture.Redis.GetDatabase().KeyTimeToLiveAsync(RedisCacheService.ScopeVersionKey(scope));
            ttl.Should().NotBeNull().And.BeLessThanOrEqualTo(RedisCacheService.ScopeVersionTtl);
        }

        [Fact]
        public async Task Entries_RoundTripAsJson_AndExpire()
        {
            var cache = Cache();
            var key = $"entry:{Guid.NewGuid()}";

            await cache.SetAsync(key, new Dictionary<string, decimal> { ["amount"] = -42.125m }, TimeSpan.FromSeconds(30));

            (await cache.GetAsync<Dictionary<string, decimal>>(key)).Should().ContainKey("amount").WhoseValue.Should().Be(-42.125m);
            (await _fixture.Redis.GetDatabase().KeyTimeToLiveAsync(key)).Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
        }

        [Fact]
        public async Task RateLimit_IsSharedAcrossReplicas_ThroughOneRedisCounter()
        {
            var options = new RedisRateLimiterOptions { PermitLimit = 3, Window = TimeSpan.FromMinutes(1) };
            var partition = Guid.NewGuid().ToString();
            var replicaA = new RedisFixedWindowRateLimiter(_fixture.Redis, "test", partition, options, NullLogger.Instance);
            var replicaB = new RedisFixedWindowRateLimiter(_fixture.Redis, "test", partition, options, NullLogger.Instance);

            var outcomes = new[]
            {
                (await replicaA.AcquireAsync()).IsAcquired,
                (await replicaB.AcquireAsync()).IsAcquired,
                (await replicaA.AcquireAsync()).IsAcquired,
                (await replicaB.AcquireAsync()).IsAcquired
            };

            outcomes.Should().Equal(true, true, true, false);
            (await _fixture.Redis.GetDatabase().KeyTimeToLiveAsync($"ratelimit:test:{partition}")).Should().NotBeNull("the window always expires");
        }

        [Fact]
        public async Task RateLimit_Rejection_RetriesAfterTheWindowsRemainingTime_NotAFullWindow()
        {
            var options = new RedisRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromSeconds(10) };
            var limiter = new RedisFixedWindowRateLimiter(_fixture.Redis, "test", Guid.NewGuid().ToString(), options, NullLogger.Instance);

            (await limiter.AcquireAsync()).IsAcquired.Should().BeTrue();
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            using var rejected = await limiter.AcquireAsync();

            rejected.IsAcquired.Should().BeFalse();
            rejected.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter).Should().BeTrue();
            retryAfter.Should().BePositive()
                .And.BeLessThanOrEqualTo(TimeSpan.FromSeconds(8.5), "1.5 s of the 10 s window had already passed");
        }
    }
}