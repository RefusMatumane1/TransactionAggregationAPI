using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Microsoft.Extensions.Hosting
{
    // Secrets live in a key/value JSON file with the same shape as appsettings.json, never in a
    // tracked file. Locally it is secrets.json in the host's content root (git-ignored); in a cluster
    // a Vault Agent renders the same structure to the path given by Secrets:FilePath.
    //
    // Precedence: appsettings.json < appsettings.{Environment}.json < secrets file < environment
    // variables < command line. The file is reloaded when Vault rotates it.
    public static class SecretsConfigurationExtensions
    {
        public const string FilePathKey = "Secrets:FilePath";
        public const string DefaultFileName = "secrets.json";

        public static TBuilder AddSecretsFile<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {
            builder.Configuration.AddSecretsFile(builder.Environment.ContentRootPath);
            return builder;
        }

        public static IConfigurationManager AddSecretsFile(this IConfigurationManager configuration, string contentRootPath)
        {
            var configuredPath = configuration[FilePathKey];
            var isConfigured = !string.IsNullOrWhiteSpace(configuredPath);
            var path = isConfigured
                ? Path.GetFullPath(configuredPath!, contentRootPath)
                : Path.Combine(contentRootPath, DefaultFileName);

            // An explicitly configured path is a deployment contract: a missing file fails start-up
            // instead of the host starting without its credentials.
            if (isConfigured && !File.Exists(path))
                throw new InvalidOperationException(
                    $"{FilePathKey} points to '{path}', which does not exist. The secrets file must be rendered before the host starts.");

            var source = new JsonConfigurationSource
            {
                Path = Path.GetFileName(path),
                Optional = !isConfigured,
                ReloadOnChange = true,
                FileProvider = new FileProviders.PhysicalFileProvider(Path.GetDirectoryName(path)!)
            };

            configuration.Sources.Insert(IndexAfterAppSettings(configuration.Sources), source);
            return configuration;
        }

        private static int IndexAfterAppSettings(IList<IConfigurationSource> sources)
        {
            var lastAppSettings = -1;
            for (var i = 0; i < sources.Count; i++)
            {
                if (sources[i] is JsonConfigurationSource { Path: { } path }
                    && Path.GetFileName(path).StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
                    lastAppSettings = i;
            }

            return lastAppSettings + 1;
        }
    }
}