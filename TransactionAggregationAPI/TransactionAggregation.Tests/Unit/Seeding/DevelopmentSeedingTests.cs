using BuildingBlocks.Application.Seeding;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Seeding
{
    public class DevelopmentSeedingTests
    {
        private sealed class Log
        {
            public List<string> Entries { get; } = [];
        }

        private sealed class Unit;

        private sealed class First(Log log, Unit unit) : IDevelopmentSeeder
        {
            public Task SeedAsync(CancellationToken cancellationToken)
            {
                log.Entries.Add($"first:{unit.GetHashCode()}");
                return Task.CompletedTask;
            }
        }

        private sealed class Second(Log log, Unit unit) : IDevelopmentSeeder
        {
            public Task SeedAsync(CancellationToken cancellationToken)
            {
                log.Entries.Add($"second:{unit.GetHashCode()}");
                return Task.CompletedTask;
            }
        }

        private sealed class Constructed : IDevelopmentSeeder
        {
            public static int Instances;

            public Constructed() => Instances++;

            public Task SeedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private static ServiceProvider Build(Action<IServiceCollection> register)
        {
            var services = new ServiceCollection();
            services.AddSingleton<Log>();
            services.AddScoped<Unit>();
            register(services);
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        }

        [Fact]
        public async Task Seeders_RunInRegistrationOrder()
        {
            using var provider = Build(s => s.AddDevelopmentSeeder<Second>().AddDevelopmentSeeder<First>());

            await provider.SeedDevelopmentDataAsync();

            provider.GetRequiredService<Log>().Entries.Select(e => e.Split(':')[0]).Should().Equal("second", "first");
        }

        [Fact]
        public async Task EachSeeder_GetsAScopeOfItsOwn_SoOneSeedersContextNeverCarriesIntoTheNext()
        {
            using var provider = Build(s => s.AddDevelopmentSeeder<First>().AddDevelopmentSeeder<Second>());

            await provider.SeedDevelopmentDataAsync();

            provider.GetRequiredService<Log>().Entries.Select(e => e.Split(':')[1]).Distinct().Should().HaveCount(2);
        }

        [Fact]
        public async Task OnlyTheSeederThatRuns_IsConstructed()
        {
            Constructed.Instances = 0;
            using var provider = Build(s => s.AddDevelopmentSeeder<Constructed>().AddDevelopmentSeeder<First>());

            await provider.SeedDevelopmentDataAsync();

            Constructed.Instances.Should().Be(1, "building every seeder for every scope would run constructors that have nothing to do");
        }

        [Fact]
        public void RegisteringASeederTwice_RegistersItOnce()
        {
            var services = new ServiceCollection().AddDevelopmentSeeder<First>().AddDevelopmentSeeder<First>();

            services.Count(d => d.ServiceType == typeof(DevelopmentSeederRegistration)).Should().Be(1);
        }

        [Fact]
        public async Task NoSeeders_IsNotAnError()
        {
            using var provider = Build(_ => { });

            var act = () => provider.SeedDevelopmentDataAsync();

            await act.Should().NotThrowAsync();
        }
    }
}
