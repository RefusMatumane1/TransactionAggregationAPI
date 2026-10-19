namespace TransactionAggregation.Worker.Messaging
{
    internal static class WorkerRegistration
    {
        private const string EnabledKey = "Enabled";

        /// <summary>Hosts <typeparamref name="TService"/> unless <c>{section}:Enabled</c> is switched off (it is on by default).</summary>
        public static IServiceCollection AddHostedServiceIfEnabled<TService>(
            this IServiceCollection services, IConfiguration configuration, string section)
            where TService : class, IHostedService =>
            configuration.GetValue($"{section}:{EnabledKey}", true)
                ? services.AddHostedService<TService>()
                : services;
    }
}
