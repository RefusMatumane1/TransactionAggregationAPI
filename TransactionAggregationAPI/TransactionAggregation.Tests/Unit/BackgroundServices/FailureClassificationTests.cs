using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Publishing;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.Errors;
using Modules.Transactions.Contracts.IntegrationEvents;
using NSubstitute;
using SharedKernel.Common.Models;
using SharedKernel.Exceptions;
using System.Net;
using TransactionAggregation.Tests.Helpers;
using TransactionAggregation.Worker.BackgroundServices;
using TransactionAggregation.Worker.Outbox;
using Xunit;

namespace TransactionAggregation.Tests.Unit.BackgroundServices
{
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
                message, SenderReturning(TransactionErrors.SourceNotAuthorizedForInstitution("FNB", "Absa")),
                InMemoryMessagingDbContextFactory.Create(), CancellationToken.None);

            message.Status.Should().Be(InboxMessageStatus.DeadLettered);
            message.Attempts.Should().Be(1, "retrying can't make one bank's delivery belong to another bank");
        }

        [Fact]
        public async Task Inbox_NotFound_IsRetriedUntilTheAttemptsAreSpent()
        {
            var message = InboxMessage.Create($"src-{Guid.NewGuid():N}", ValidPayload);
            var messaging = InMemoryMessagingDbContextFactory.Create();
            var inbox = Inbox(maxAttempts: 2);

            for (var i = 0; i < 2; i++)
                await inbox.ProcessMessageAsync(message, SenderReturning(Error.NotFound("WebhookSource", "src-1")), messaging, CancellationToken.None);

            message.Status.Should().Be(InboxMessageStatus.DeadLettered);
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
            var message = OutboxMessage.Create(TransactionRecorded.EventType, "{}", schemaVersion: 99);
            var publisher = Substitute.For<IIntegrationEventPublisher>();
            var sut = new OutboxDispatcherBackgroundService(
                Substitute.For<IServiceScopeFactory>(),
                NullLogger<OutboxDispatcherBackgroundService>.Instance,
                Options.Create(new OutboxOptions()));

            await sut.ProcessMessageAsync(message, new TransactionRecordedHandler(publisher));

            message.Status.Should().Be(OutboxMessageStatus.DeadLettered);
            message.LastError.Should().Contain("schema version 99");
            await publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, FailureKind.Permanent)]
        [InlineData(HttpStatusCode.Forbidden, FailureKind.Permanent)]
        [InlineData(HttpStatusCode.NotFound, FailureKind.Permanent)]
        [InlineData(HttpStatusCode.RequestTimeout, FailureKind.Transient)]
        [InlineData(HttpStatusCode.TooManyRequests, FailureKind.Transient)]
        [InlineData(HttpStatusCode.InternalServerError, FailureKind.Transient)]
        [InlineData(HttpStatusCode.ServiceUnavailable, FailureKind.Transient)]
        public void Classify_HttpFailure_ByStatus(HttpStatusCode status, FailureKind expected) =>
            FailureClassifier.Classify(new HttpRequestException("x", null, status)).Should().Be(expected);

        [Fact]
        public void Classify_PermanentDelivery_IsPermanent() =>
            FailureClassifier.Classify(new PermanentDeliveryException("refused")).Should().Be(FailureKind.Permanent);

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
}