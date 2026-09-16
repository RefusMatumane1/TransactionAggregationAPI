using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Common.Enums;
using TransactionAggregation.Application.Features.WebhookSources.Commands.RotateWebhookSourceKey;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Persistence;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class RotateWebhookSourceKeyCommandHandlerTests
{
    private static RotateWebhookSourceKeyCommandHandler BuildHandler(ApplicationDbContext ctx)
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        return new RotateWebhookSourceKeyCommandHandler(ctx, userContext, NullLogger<RotateWebhookSourceKeyCommandHandler>.Instance);
    }

    private static async Task<(WebhookSource Source, string OriginalKey)> SeedSourceAsync(ApplicationDbContext ctx, string name = "stitch")
    {
        var (source, key) = WebhookSource.Create(name);
        ctx.WebhookSources.Add(source);
        await ctx.SaveChangesAsync();
        return (source, key);
    }

    [Fact]
    public async Task Handle_ExistingSource_ReturnsANewKeyDifferentFromTheOriginal()
    {
        var context = InMemoryDbContextFactory.Create();
        var (source, originalKey) = await SeedSourceAsync(context);
        var handler = BuildHandler(context);

        var result = await handler.Handle(new RotateWebhookSourceKeyCommand(source.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(originalKey);
    }

    [Fact]
    public async Task Handle_ExistingSource_OldKeyNoLongerMatchesStoredHash()
    {
        var context = InMemoryDbContextFactory.Create();
        var (source, originalKey) = await SeedSourceAsync(context);
        var handler = BuildHandler(context);

        await handler.Handle(new RotateWebhookSourceKeyCommand(source.Id.Value), CancellationToken.None);

        var stored = context.WebhookSources.Single();
        WebhookSource.HashKey(originalKey).Should().NotBe(stored.KeyHash);
    }

    [Fact]
    public async Task Handle_ExistingSource_NewKeyMatchesStoredHash()
    {
        var context = InMemoryDbContextFactory.Create();
        var (source, _) = await SeedSourceAsync(context);
        var handler = BuildHandler(context);

        var result = await handler.Handle(new RotateWebhookSourceKeyCommand(source.Id.Value), CancellationToken.None);

        var stored = context.WebhookSources.Single();
        WebhookSource.HashKey(result.Value).Should().Be(stored.KeyHash);
    }

    [Fact]
    public async Task Handle_UnknownId_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(new RotateWebhookSourceKeyCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}