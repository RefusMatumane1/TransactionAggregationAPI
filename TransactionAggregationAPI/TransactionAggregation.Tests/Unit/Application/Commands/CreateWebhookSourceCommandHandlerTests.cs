using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Common.Enums;
using TransactionAggregation.Application.Features.WebhookSources.Commands.CreateWebhookSource;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Persistence;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class CreateWebhookSourceCommandHandlerTests
{
    private static CreateWebhookSourceCommandHandler BuildHandler(ApplicationDbContext ctx)
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        return new CreateWebhookSourceCommandHandler(ctx, userContext, NullLogger<CreateWebhookSourceCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_NewName_CreatesActiveSourceAndReturnsAUsableKey()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(new CreateWebhookSourceCommand("stitch"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("stitch");
        result.Value.ApiKey.Should().NotBeNullOrWhiteSpace();

        var stored = context.WebhookSources.Single();
        stored.Id.Value.Should().Be(result.Value.Id);
        stored.IsActive.Should().BeTrue();
        WebhookSource.HashKey(result.Value.ApiKey).Should().Be(stored.KeyHash);
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsConflictAndDoesNotPersistASecondRow()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);
        await handler.Handle(new CreateWebhookSourceCommand("stitch"), CancellationToken.None);

        var result = await handler.Handle(new CreateWebhookSourceCommand("stitch"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        context.WebhookSources.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_DifferentNames_BothSucceedWithDistinctKeys()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var a = await handler.Handle(new CreateWebhookSourceCommand("source-a"), CancellationToken.None);
        var b = await handler.Handle(new CreateWebhookSourceCommand("source-b"), CancellationToken.None);

        a.IsSuccess.Should().BeTrue();
        b.IsSuccess.Should().BeTrue();
        a.Value.ApiKey.Should().NotBe(b.Value.ApiKey);
        context.WebhookSources.Should().HaveCount(2);
    }
}