using FluentAssertions;
using Modules.Customers.Infrastructure.Authentication;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Services;

/// <summary>
/// The realm requires a first and a last name; a user created without either can't sign in
/// with a password and is sent to a profile form on first browser login.
/// </summary>
public class KeycloakNameSplitTests
{
    [Theory]
    [InlineData("Thabo Mokoena", "Thabo", "Mokoena")]
    [InlineData("Pieter van der Merwe", "Pieter", "van der Merwe")]
    [InlineData("  Lerato   Dlamini  ", "Lerato", "Dlamini")]
    [InlineData("Zola", "Zola", "Zola")]
    public void SplitName_AlwaysGivesBothNames(string name, string first, string last)
    {
        var (firstName, lastName) = KeycloakAdminClient.SplitName(name);

        firstName.Should().Be(first);
        lastName.Should().Be(last);
    }
}