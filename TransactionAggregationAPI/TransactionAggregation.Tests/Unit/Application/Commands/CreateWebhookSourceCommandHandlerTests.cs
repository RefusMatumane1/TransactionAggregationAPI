using BuildingBlocks.Application.Abstractions.Authentication;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Notifications.Application.Contracts;
using Modules.Notifications.Contracts;
using Modules.WebhookSources.Application.Features.CreateWebhookSource;
using Modules.WebhookSources.Domain;
using Modules.WebhookSources.Infrastructure.Persistence;
using NSubstitute;
using SharedKernel.Common.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands
{
    public class CreateWebhookSourceCommandHandlerTests
    {
        private static CreateWebhookSourceCommandHandler BuildHandler(WebhookSourcesDbContext ctx, IDuplicateAlertWebhooks? notifications = null)
        {
            var userContext = Substitute.For<IUserContext>();
            userContext.UserId.Returns(Guid.NewGuid());
            return new CreateWebhookSourceCommandHandler(
                ctx, userContext, notifications ?? Substitute.For<IDuplicateAlertWebhooks>(), NullLogger<CreateWebhookSourceCommandHandler>.Instance);
        }

        private static IDuplicateAlertWebhooks Notifications(AlertWebhookOutcome outcome = AlertWebhookOutcome.Set)
        {
            var notifications = Substitute.For<IDuplicateAlertWebhooks>();
            notifications.SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(outcome);
            return notifications;
        }

        [Fact]
        public async Task Handle_WithANotificationUrl_ConfiguresItForTheNewBank()
        {
            var notifications = Notifications();
            var handler = BuildHandler(InMemoryWebhookSourcesDbContextFactory.Create(), notifications);

            var result = await handler.Handle(
                new CreateWebhookSourceCommand("Nedbank", "Nedbank", "#007A4D", "  https://hooks.nedbank.example/notify  "), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.NotificationUrlConfigured.Should().BeTrue();
            await notifications.Received(1).SetAsync("Nedbank", "https://hooks.nedbank.example/notify", Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Handle_WithoutANotificationUrl_ConfiguresNothing(string? url)
        {
            var notifications = Notifications();
            var handler = BuildHandler(InMemoryWebhookSourcesDbContextFactory.Create(), notifications);

            var result = await handler.Handle(new CreateWebhookSourceCommand("Nedbank", "Nedbank", "#007A4D", url), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.NotificationUrlConfigured.Should().BeFalse();
            await notifications.DidNotReceiveWithAnyArgs().SetAsync(default!, default!, default);
        }

        [Fact]
        public async Task Handle_WhenTheUrlCannotBeStored_StillCreatesTheBank_AndSaysTheUrlIsNotConfigured()
        {
            var notifications = Substitute.For<IDuplicateAlertWebhooks>();
            notifications.SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns<Task<AlertWebhookOutcome>>(_ => throw new InvalidOperationException("database unavailable"));
            var context = InMemoryWebhookSourcesDbContextFactory.Create();

            var result = await BuildHandler(context, notifications).Handle(
                new CreateWebhookSourceCommand("Nedbank", "Nedbank", "#007A4D", "https://hooks.nedbank.example/notify"), CancellationToken.None);

            result.IsSuccess.Should().BeTrue("the bank exists and its one-time key must still be returned");
            result.Value.ApiKey.Should().NotBeNullOrWhiteSpace();
            result.Value.NotificationUrlConfigured.Should().BeFalse();
            context.WebhookSources.Should().ContainSingle();
        }

        [Theory]
        [InlineData(AlertWebhookOutcome.InvalidUrl)]
        [InlineData(AlertWebhookOutcome.UnknownBank)]
        public async Task Handle_WhenTheUrlIsRefused_TheBankIsStillCreated(AlertWebhookOutcome outcome)
        {
            var context = InMemoryWebhookSourcesDbContextFactory.Create();

            var result = await BuildHandler(context, Notifications(outcome)).Handle(
                new CreateWebhookSourceCommand("Nedbank", "Nedbank", "#007A4D", "https://hooks.nedbank.example/notify"), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.NotificationUrlConfigured.Should().BeFalse();
            context.WebhookSources.Should().ContainSingle();
        }

        [Fact]
        public async Task Handle_DuplicateName_DoesNotTouchNotifications()
        {
            var notifications = Notifications();
            var handler = BuildHandler(InMemoryWebhookSourcesDbContextFactory.Create(), notifications);
            await handler.Handle(new CreateWebhookSourceCommand("Nedbank", "Nedbank", "#007A4D"), CancellationToken.None);

            await handler.Handle(new CreateWebhookSourceCommand("nedbank", "Other", "#007A4D", "https://hooks.other.example/x"), CancellationToken.None);

            await notifications.DidNotReceiveWithAnyArgs().SetAsync(default!, default!, default);
        }

        [Theory]
        [InlineData("https://hooks.nedbank.example/notify", true)]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("   ", true)]
        [InlineData("http://hooks.nedbank.example/notify", false)]
        [InlineData("https://localhost/notify", false)]
        [InlineData("https://10.0.0.1/notify", false)]
        [InlineData("https://user:pass@hooks.nedbank.example/notify", false)]
        [InlineData("not a url", false)]
        public void Validator_AcceptsAMissingNotificationUrl_AndRefusesAnUnsafeOne(string? url, bool valid)
        {
            var validator = new CreateWebhookSourceCommandValidator(new DuplicateAlertWebhooks(Substitute.For<ISender>()));

            var result = validator.Validate(new CreateWebhookSourceCommand("Nedbank", "Nedbank", "#007A4D", url));

            result.IsValid.Should().Be(valid);
            if (!valid)
                result.Errors.Should().ContainSingle(e => e.PropertyName == "NotificationUrl");
        }

        [Fact]
        public async Task Handle_NewName_CreatesActiveSourceAndReturnsAUsableKey()
        {
            var context = InMemoryWebhookSourcesDbContextFactory.Create();
            var handler = BuildHandler(context);

            var result = await handler.Handle(new CreateWebhookSourceCommand("Nedbank", "Nedbank", "#007A4D"), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Code.Should().Be("Nedbank");
            result.Value.DisplayName.Should().Be("Nedbank");
            result.Value.Color.Should().Be("#007A4D");
            result.Value.ApiKey.Should().NotBeNullOrWhiteSpace();

            var stored = context.WebhookSources.Single();
            stored.Id.Value.Should().Be(result.Value.Id);
            stored.IsActive.Should().BeTrue();
            WebhookSource.HashKey(result.Value.ApiKey).Should().Be(stored.KeyHash);
        }

        [Fact]
        public async Task Handle_DuplicateName_ReturnsConflictAndDoesNotPersistASecondRow()
        {
            var context = InMemoryWebhookSourcesDbContextFactory.Create();
            var handler = BuildHandler(context);
            await handler.Handle(new CreateWebhookSourceCommand("Nedbank", "Nedbank", "#007A4D"), CancellationToken.None);

            var result = await handler.Handle(new CreateWebhookSourceCommand("Nedbank", "Nedbank", "#007A4D"), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Conflict);
            context.WebhookSources.Should().HaveCount(1);
        }

        [Fact]
        public async Task Handle_DifferentNames_BothSucceedWithDistinctKeys()
        {
            var context = InMemoryWebhookSourcesDbContextFactory.Create();
            var handler = BuildHandler(context);

            var a = await handler.Handle(new CreateWebhookSourceCommand("source-a", "Source A", "#111111"), CancellationToken.None);
            var b = await handler.Handle(new CreateWebhookSourceCommand("source-b", "Source B", "#222222"), CancellationToken.None);

            a.IsSuccess.Should().BeTrue();
            b.IsSuccess.Should().BeTrue();
            a.Value.ApiKey.Should().NotBe(b.Value.ApiKey);
            context.WebhookSources.Should().HaveCount(2);
        }
    }
}