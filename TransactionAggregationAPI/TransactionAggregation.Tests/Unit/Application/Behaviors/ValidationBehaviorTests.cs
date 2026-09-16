using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Behaviors;
using TransactionAggregation.Application.Common.Models;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Behaviors;

public class ValidationBehaviorTests
{
    public sealed record FakeCommand(string Value) : ICommand<Guid>;

    [Fact]
    public async Task Handle_ValidatorFails_ReturnsFailureResultInsteadOfThrowing()
    {
        var failingValidator = Substitute.For<IValidator<FakeCommand>>();
        failingValidator
            .ValidateAsync(Arg.Any<ValidationContext<FakeCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Value", "must not be empty")]));

        var behavior = new ValidationBehavior<FakeCommand, Result<Guid>>([failingValidator]);

        var act = () => behavior.Handle(new FakeCommand(""), _ => throw new Exception("should not reach handler"), CancellationToken.None);

        var result = await act.Should().NotThrowAsync();
        result.Subject.IsFailure.Should().BeTrue();
        result.Subject.Error.Description.Should().Contain("must not be empty");
    }

    [Fact]
    public async Task Handle_ValidatorPasses_InvokesNext()
    {
        var passingValidator = Substitute.For<IValidator<FakeCommand>>();
        passingValidator
            .ValidateAsync(Arg.Any<ValidationContext<FakeCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var behavior = new ValidationBehavior<FakeCommand, Result<Guid>>([passingValidator]);
        var expected = Result.Success(Guid.NewGuid());

        var result = await behavior.Handle(new FakeCommand("ok"), _ => Task.FromResult(expected), CancellationToken.None);

        result.Should().Be(expected);
    }
}