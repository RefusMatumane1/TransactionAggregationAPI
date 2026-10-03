using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Application.Caching;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Common.Models;
using System.Text.Json;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Behaviors
{
    public class CachingBehaviorTests
    {
        public sealed record Item(Guid Id, decimal Amount, string Currency);

        public sealed record CachedQuery(Guid ScopeId, int Page = 1) : IRequest<Result<List<Item>>>, ICacheableQuery
        {
            public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);
            public string CacheScope => $"transactions:{ScopeId}";
        }

        private sealed class JsonCache : ICacheService
        {
            private readonly Dictionary<string, string> _entries = [];
            private readonly Dictionary<string, long> _versions = [];

            public bool Available { get; set; } = true;
            public IReadOnlyCollection<string> Keys => _entries.Keys;

            public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
                Task.FromResult(_entries.TryGetValue(key, out var json) ? JsonSerializer.Deserialize<T>(json) : null);

            public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
            {
                _entries[key] = JsonSerializer.Serialize(value);
                return Task.CompletedTask;
            }

            public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task<long?> GetScopeVersionAsync(string scope, CancellationToken cancellationToken = default) =>
                Task.FromResult<long?>(Available ? _versions.GetValueOrDefault(scope) : null);

            public Task InvalidateScopeAsync(string scope, CancellationToken cancellationToken = default)
            {
                _versions[scope] = _versions.GetValueOrDefault(scope) + 1;
                return Task.CompletedTask;
            }
        }

        private sealed class CountingHandler(List<Item> items)
        {
            public int Calls { get; private set; }

            public Task<Result<List<Item>>> Handle(CancellationToken _)
            {
                Calls++;
                return Task.FromResult(Result.Success(items));
            }
        }

        private static CachingBehavior<CachedQuery, Result<List<Item>>> BehaviorOver(JsonCache cache) =>
            new(cache, NullLogger<CachingBehavior<CachedQuery, Result<List<Item>>>>.Instance);

        [Fact]
        public async Task SuccessfulResult_IsServedFromTheCache_WithItsValueIntact()
        {
            var behavior = BehaviorOver(new JsonCache());
            var query = new CachedQuery(Guid.NewGuid());
            var items = new List<Item> { new(Guid.NewGuid(), -42.125m, "KWD") };
            var handler = new CountingHandler(items);

            await behavior.Handle(query, handler.Handle, CancellationToken.None);
            var cached = await behavior.Handle(query, handler.Handle, CancellationToken.None);

            handler.Calls.Should().Be(1, "the second call is a cache hit");
            cached.IsSuccess.Should().BeTrue();
            cached.Value.Should().BeEquivalentTo(items);
        }

        [Fact]
        public async Task FailedResult_IsNeverCached()
        {
            var behavior = BehaviorOver(new JsonCache());
            var query = new CachedQuery(Guid.NewGuid());
            var handlerCalls = 0;

            Task<Result<List<Item>>> Handler(CancellationToken _)
            {
                handlerCalls++;
                return Task.FromResult(Result.Failure<List<Item>>(Error.NotFound("Item", query.ScopeId)));
            }

            await behavior.Handle(query, Handler, CancellationToken.None);
            await behavior.Handle(query, Handler, CancellationToken.None);

            handlerCalls.Should().Be(2, "a not-found must not be remembered, or a later create would stay invisible");
        }

        [Fact]
        public async Task InvalidatingTheScope_MakesEveryCachedVariantInItMiss()
        {
            var cache = new JsonCache();
            var behavior = BehaviorOver(cache);
            var scopeId = Guid.NewGuid();
            var handler = new CountingHandler([]);

            await behavior.Handle(new CachedQuery(scopeId, Page: 1), handler.Handle, CancellationToken.None);
            await behavior.Handle(new CachedQuery(scopeId, Page: 2), handler.Handle, CancellationToken.None);
            await cache.InvalidateScopeAsync($"transactions:{scopeId}");
            await behavior.Handle(new CachedQuery(scopeId, Page: 1), handler.Handle, CancellationToken.None);
            await behavior.Handle(new CachedQuery(scopeId, Page: 2), handler.Handle, CancellationToken.None);

            handler.Calls.Should().Be(4, "one version bump invalidates every page of the scope without deleting any key");
        }

        [Fact]
        public async Task OtherScopes_AreUnaffectedByAnInvalidation()
        {
            var cache = new JsonCache();
            var behavior = BehaviorOver(cache);
            var handler = new CountingHandler([]);
            var scopeA = new CachedQuery(Guid.NewGuid());

            await behavior.Handle(scopeA, handler.Handle, CancellationToken.None);
            await cache.InvalidateScopeAsync($"transactions:{Guid.NewGuid()}");
            await behavior.Handle(scopeA, handler.Handle, CancellationToken.None);

            handler.Calls.Should().Be(1);
        }

        [Fact]
        public async Task CacheUnavailable_RunsTheQueryUncached_AndWritesNothing()
        {
            var cache = new JsonCache { Available = false };
            var behavior = BehaviorOver(cache);
            var handler = new CountingHandler([]);
            var query = new CachedQuery(Guid.NewGuid());

            await behavior.Handle(query, handler.Handle, CancellationToken.None);
            await behavior.Handle(query, handler.Handle, CancellationToken.None);

            handler.Calls.Should().Be(2, "without a scope version a cached entry could be stale, so the cache is bypassed");
            cache.Keys.Should().BeEmpty();
        }

        [Fact]
        public async Task CacheKey_IsBoundedAndCarriesScopeAndVersion()
        {
            var cache = new JsonCache();
            var behavior = BehaviorOver(cache);
            var scopeId = Guid.NewGuid();
            var handler = new CountingHandler([]);

            await behavior.Handle(new CachedQuery(scopeId), handler.Handle, CancellationToken.None);

            var key = cache.Keys.Should().ContainSingle().Subject;
            key.Should().StartWith($"transactions:{scopeId}:v0:{nameof(CachedQuery)}:");
            key.Length.Should().BeLessThan(200);
        }
    }
}