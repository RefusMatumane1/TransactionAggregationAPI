using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Common.Enums;
using TransactionAggregation.Application.Features.WebhookSources.Commands.ActivateWebhookSource;
using TransactionAggregation.Application.Features.WebhookSources.Commands.DeactivateWebhookSource;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Persistence;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class ActivateDeactivateWebhookSourceCommandHandlerTests
{
    private static IUserContext BuildUserContext()
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        return userContext;
    }

    private static async Task<WebhookSource> SeedSourceAsync(ApplicationDbContext ctx, string name = "stitch")
    {
        var (source, _) = WebhookSource.Create(name);
        ctx.WebhookSources.Add(source);
        await ctx.SaveChangesAsync();
        return source;
    }

    [Fact]
    public async Task Deactivate_ExistingActiveSource_SetsIsActiveFalse()
    {
        var context = InMemoryDbContextFactory.Create();
        var source = await SeedSourceAsync(context);
        var handler = new DeactivateWebhookSourceCommandHandler(
            context, BuildUserContext(), NullLogger<DeactivateWebhookSourceCommandHandler>.Instance);

        var result = await handler.Handle(new DeactivateWebhookSourceCommand(source.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.WebhookSources.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_UnknownId_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new DeactivateWebhookSourceCommandHandler(
            context, BuildUserContext(), NullLogger<DeactivateWebhookSourceCommandHandler>.Instance);

        var result = await handler.Handle(new DeactivateWebhookSourceCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Activate_DeactivatedSource_SetsIsActiveTrue()
    {
        var context = InMemoryDbContextFactory.Create();
        var source = await SeedSourceAsync(context);
        source.Deactivate();
        await context.SaveChangesAsync();
        var handler = new ActivateWebhookSourceCommandHandler(
            context, BuildUserContext(), NullLogger<ActivateWebhookSourceCommandHandler>.Instance);

        var result = await handler.Handle(new ActivateWebhookSourceCommand(source.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.WebhookSources.Single().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Activate_UnknownId_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new ActivateWebhookSourceCommandHandler(
            context, BuildUserContext(), NullLogger<ActivateWebhookSourceCommandHandler>.Instance);

        var result = await handler.Handle(new ActivateWebhookSourceCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}