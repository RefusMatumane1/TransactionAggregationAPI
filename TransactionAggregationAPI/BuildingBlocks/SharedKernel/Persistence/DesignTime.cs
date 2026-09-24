using MediatR;

namespace SharedKernel.Persistence
{
    /// <summary>
    /// Shared by every module's IDesignTimeDbContextFactory (used only by `dotnet ef`).
    /// The connection string comes from the same variable the hosts read at runtime and is
    /// never defaulted in source: a missing value fails fast with instructions instead of
    /// silently pointing tooling at a guessed server with a guessed password.
    /// </summary>
    public static class DesignTime
    {
        public const string ConnectionStringVariable = "ConnectionStrings__transactiondb";

        public static string ConnectionString =>
            Environment.GetEnvironmentVariable(ConnectionStringVariable) is { Length: > 0 } value
                ? value
                : throw new InvalidOperationException(
                    $"Set the {ConnectionStringVariable} environment variable to run EF Core tooling, e.g. " +
                    $"{ConnectionStringVariable}=\"Host=localhost;Database=transactiondb;Username=<user>;Password=<password>\". " +
                    "`dotnet ef migrations add` never connects, so any syntactically valid value works for it.");

        /// <summary>Design-time contexts never save, so they never publish domain events.</summary>
        public static IMediator NoOpMediator { get; } = new NoOpMediatorImpl();

        private sealed class NoOpMediatorImpl : IMediator
        {
            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
                => Task.FromResult<TResponse>(default!);

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
                => Task.CompletedTask;

            public Task<object?> Send(object request, CancellationToken cancellationToken = default)
                => Task.FromResult<object?>(null);

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
                => AsyncEnumerable.Empty<TResponse>();

            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
                => AsyncEnumerable.Empty<object?>();

            public Task Publish(object notification, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
                where TNotification : INotification
                => Task.CompletedTask;
        }
    }
}