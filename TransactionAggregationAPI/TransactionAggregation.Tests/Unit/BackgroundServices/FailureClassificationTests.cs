using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Errors;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Infrastructure.BackgroundServices;
using NSubstitute;
using SharedKernel.Common.Interfaces;
using SharedKernel.Common.Models;
using SharedKernel.Exceptions;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.BackgroundServices;

/// <summary>
/// Permanent failures (refused, invalid, undecodable) are dead-lettered on the first attempt;
/// transient ones (database, not-found-yet, conflicts) keep the bounded retry budget.
/// </summary>
public class FailureClassificationTests
{
    private const string ValidPayload = """{"ExternalAccountId":"acc-1","Transactions":[]}""";

    private static InboxDispatcherBackgroundService Inbox(int maxAttempts = 5) => new(
        Substitute.For<IServiceScopeFactory>(),
        NullLogger<InboxDispatcherBackgroundService>.Instance,
        Options.Create(new InboxOptions { MaxAttempts = maxAttempts }));

    private static ISender SenderReturning(Error error)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<IRequest<Result<int>>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<int>(error));
        return sender;
    }

    [Fact]
    public async Task Inbox_SourceNotAuthorized_IsDeadLetteredOnTheFirstAttempt()
    {
        var message = InboxMessage.Create($"src-{Guid.NewGuid():N}", ValidPayload);

        await Inbox().ProcessMessageAsync(
            message, SenderReturning(TransactionErrors.SourceNotAuthorizedForAccount("src", "acc-1")),
            InMemoryMessagingDbContextFactory.Create(), CancellationToken.None);

        message.Status.Should().Be(InboxMessageStatus.DeadLettered);
        message.Attempts.Should().Be(1, "retrying can't change which institutions the source may write to");
    }

    [Fact]
    public async Task Inbox_BankLinkNotFoundYet_StaysPendingForRetry()
    {
        var message = InboxMessage.Create($"src-{Guid.NewGuid():N}", ValidPayload);

        await Inbox().ProcessMessageAsync(
            message, SenderReturning(Error.NotFound("BankLink", "acc-1")),
            InMemoryMessagingDbContextFactory.Create(), CancellationToken.None);

        message.Status.Should().Be(InboxMessageStatus.Pending, "the link may be activated before the next attempt");
        message.NextAttemptAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Inbox_UndecodablePayload_IsDeadLetteredOnTheFirstAttempt()
    {
        var message = InboxMessage.Create($"src-{Guid.NewGuid():N}", "null");

        await Inbox().ProcessMessageAsync(
            message, Substitute.For<ISender>(), InMemoryMessagingDbContextFactory.Create(), CancellationToken.None);

        message.Status.Should().Be(InboxMessageStatus.DeadLettered);
    }

    [Fact]
    public async Task Inbox_TransientException_StaysPendingForRetry()
    {
        var message = InboxMessage.Create($"src-{Guid.NewGuid():N}", ValidPayload);
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<IRequest<Result<int>>>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<int>>>(_ => throw new TimeoutException("db timeout"));

        await Inbox().ProcessMessageAsync(message, sender, InMemoryMessagingDbContextFactory.Create(), CancellationToken.None);

        message.Status.Should().Be(InboxMessageStatus.Pending);
    }

    [Fact]
    public void MarkFailed_OverlongError_IsTruncatedToTheColumnLength()
    {
        var message = InboxMessage.Create("src", ValidPayload);

        message.MarkFailed(new string('x', 10_000), TimeSpan.FromSeconds(1), maxAttempts: 5);

        message.LastError!.Length.Should().Be(InboxMessage.MaxErrorLength,
            "an over-long error must never make the status update itself fail and wedge the message");
    }

    [Fact]
    public async Task Outbox_KnownTypeWithNewerSchemaVersion_IsDeadLettered_NotMisread()
    {
        var message = OutboxMessage.Create(OutboxMessageTypes.TransactionSynced, "{}", schemaVersion: 99);
        var sut = new OutboxDispatcherBackgroundService(
            Substitute.For<IServiceScopeFactory>(),
            NullLogger<OutboxDispatcherBackgroundService>.Instance,
            Options.Create(new OutboxOptions()));

        await sut.ProcessMessageAsync(
            message,
            Substitute.For<ITransactionsDbContext>(),
            Substitute.For<ICacheService>(),
            Substitute.For<IAnalyticsService>(),
            Substitute.For<INotificationService>(),
            Substitute.For<IAuditTrail>(),
            CancellationToken.None);

        message.Status.Should().Be(OutboxMessageStatus.DeadLettered);
        message.LastError.Should().Contain("schema version 99");
    }

    [Theory]
    [InlineData(typeof(PoisonMessageException), FailureKind.Permanent)]
    [InlineData(typeof(DomainException), FailureKind.Permanent)]
    [InlineData(typeof(TimeoutException), FailureKind.Transient)]
    [InlineData(typeof(InvalidOperationException), FailureKind.Transient)]
    public void Classify_Exception(Type exceptionType, FailureKind expected)
    {
        var exception = exceptionType == typeof(PoisonMessageException)
            ? new PoisonMessageException("x")
            : (Exception)Activator.CreateInstance(exceptionType, "x")!;

        FailureClassifier.Classify(exception).Should().Be(expected);
    }
}