using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using BuildingBlocks.Messaging.Persistence;

namespace TransactionAggregation.Persistence;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string DesignTimeConnectionString = "Host=localhost;Database=transactiondb;Username=postgres;Password=postgres";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(DesignTimeConnectionString);

        var messagingOptionsBuilder = new DbContextOptionsBuilder<MessagingDbContext>();
        messagingOptionsBuilder.UseNpgsql(DesignTimeConnectionString);
        var messagingDbContext = new MessagingDbContext(messagingOptionsBuilder.Options, new NoOpMediator());

        return new ApplicationDbContext(optionsBuilder.Options, new NoOpMediator(), messagingDbContext);
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
