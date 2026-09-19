using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modules.WebhookSources.Persistence;

public class WebhookSourcesDbContextFactory : IDesignTimeDbContextFactory<WebhookSourcesDbContext>
{
    public WebhookSourcesDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WebhookSourcesDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=transactiondb;Username=postgres;Password=postgres");

        return new WebhookSourcesDbContext(optionsBuilder.Options, new NoOpMediator());
    }

    private sealed class NoOpMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Task.FromResult<TResponse>(default!);

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => Task.CompletedTask;

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(null);

        public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<object?> CreateStream(object request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
            => Task.CompletedTask;
    }
}
