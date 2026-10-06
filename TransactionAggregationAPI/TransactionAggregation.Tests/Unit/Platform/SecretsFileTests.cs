using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Platform
{
    public sealed class SecretsFileTests : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("secrets-test-").FullName;

        public void Dispose() => Directory.Delete(_root, recursive: true);

        private ConfigurationManager ConfigurationWith(Action<ConfigurationManager>? beforeSecrets = null)
        {
            File.WriteAllText(Path.Combine(_root, "appsettings.json"), """{ "Db": "from-appsettings", "Only": "appsettings" }""");
            var configuration = new ConfigurationManager();
            configuration.SetBasePath(_root);
            configuration.AddJsonFile("appsettings.json");
            beforeSecrets?.Invoke(configuration);
            return configuration;
        }

        [Fact]
        public void SecretsFile_OverridesAppSettings_ButNotEnvironmentVariables()
        {
            File.WriteAllText(Path.Combine(_root, "secrets.json"), """{ "Db": "from-secrets", "Env": "from-secrets" }""");
            var configuration = ConfigurationWith(c => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Env"] = "from-environment" }));

            configuration.AddSecretsFile(_root);

            configuration["Db"].Should().Be("from-secrets");
            configuration["Only"].Should().Be("appsettings");
            configuration["Env"].Should().Be("from-environment", "the environment still wins, so a deployment can override a single value");
        }

        [Fact]
        public void NoSecretsFile_Locally_IsFine_TheRequiredSettingsFailFastLater()
        {
            var configuration = ConfigurationWith();

            configuration.AddSecretsFile(_root);

            configuration["Db"].Should().Be("from-appsettings");
        }

        [Fact]
        public void AConfiguredSecretsPathThatDoesNotExist_FailsStartUp()
        {
            var configuration = ConfigurationWith(c => c.AddInMemoryCollection(
                new Dictionary<string, string?> { [SecretsConfigurationExtensions.FilePathKey] = "/vault/secrets/missing.json" }));

            var act = () => configuration.AddSecretsFile(_root);

            act.Should().Throw<InvalidOperationException>().WithMessage("*missing.json*");
        }

        [Fact]
        public void AConfiguredSecretsPath_IsRead_AndReloadedWhenVaultRotatesIt()
        {
            var vaultDirectory = Directory.CreateDirectory(Path.Combine(_root, "vault")).FullName;
            var rendered = Path.Combine(vaultDirectory, "secrets.json");
            File.WriteAllText(rendered, """{ "Db": "v1" }""");
            var configuration = ConfigurationWith(c => c.AddInMemoryCollection(
                new Dictionary<string, string?> { [SecretsConfigurationExtensions.FilePathKey] = rendered }));
            configuration.AddSecretsFile(_root);
            configuration["Db"].Should().Be("v1");

            using var reloaded = new ManualResetEventSlim();
            using var registration = ((IConfiguration)configuration).GetReloadToken().RegisterChangeCallback(_ => reloaded.Set(), null);
            File.WriteAllText(rendered, """{ "Db": "v2" }""");

            reloaded.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("the file is watched");
            configuration["Db"].Should().Be("v2");
        }
    }
}