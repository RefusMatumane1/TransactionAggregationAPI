using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Infrastructure.Services;
using TransactionAggregation.Persistence;
using Xunit;

namespace TransactionAggregation.Tests.Architecture;

/// <summary>
/// Enforces clean-architecture layer dependencies:
///   Domain  ←  Application  ←  Infrastructure
///                           ←  Persistence
/// No inner layer may reference an outer layer.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly         = typeof(Transaction).Assembly;
    private static readonly Assembly ApplicationAssembly    = typeof(TransactionAggregation.Application.DependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(TransactionAggregator).Assembly;
    private static readonly Assembly PersistenceAssembly    = typeof(ApplicationDbContext).Assembly;

    private const string DomainNs         = "TransactionAggregation.Domain";
    private const string ApplicationNs    = "TransactionAggregation.Application";
    private const string InfrastructureNs = "TransactionAggregation.Infrastructure";
    private const string PersistenceNs    = "TransactionAggregation.Persistence";

    // ── Domain layer isolation ─────────────────────────────────────────────────

    [Fact]
    public void Domain_ShouldNot_DependOn_Application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should().NotHaveDependencyOn(ApplicationNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain is the innermost layer and must have no knowledge of Application");
    }

    [Fact]
    public void Domain_ShouldNot_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should().NotHaveDependencyOn(InfrastructureNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain must not depend on Infrastructure");
    }

    [Fact]
    public void Domain_ShouldNot_DependOn_Persistence()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should().NotHaveDependencyOn(PersistenceNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain must not depend on Persistence");
    }

    // ── Application layer isolation ────────────────────────────────────────────

    [Fact]
    public void Application_ShouldNot_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should().NotHaveDependencyOn(InfrastructureNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application must not depend on Infrastructure; use interfaces instead");
    }

    [Fact]
    public void Application_ShouldNot_DependOn_Persistence()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should().NotHaveDependencyOn(PersistenceNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application must not depend on Persistence; use IApplicationDbContext instead");
    }

    // ── Infrastructure layer isolation ─────────────────────────────────────────

    [Fact]
    public void Infrastructure_ShouldNot_DependOn_Persistence()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .Should().NotHaveDependencyOn(PersistenceNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Infrastructure must not depend on Persistence");
    }

    // ── Naming conventions ─────────────────────────────────────────────────────

    [Fact]
    public void CommandHandlers_ShouldHaveNameEndingWith_CommandHandler()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().HaveNameEndingWith("CommandHandler")
            .Should().HaveNameEndingWith("CommandHandler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "All command handlers must follow the *CommandHandler naming convention");
    }

    [Fact]
    public void QueryHandlers_ShouldHaveNameEndingWith_QueryHandler()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().HaveNameEndingWith("QueryHandler")
            .Should().HaveNameEndingWith("QueryHandler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ApplicationInterfaces_ShouldStartWith_I()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("TransactionAggregation.Application.Common.Interfaces")
            .And().AreInterfaces()
            .Should().HaveNameStartingWith("I")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "All interfaces in Application.Common.Interfaces must follow the I-prefix convention");
    }

    // ── Domain structural rules ────────────────────────────────────────────────

    [Fact]
    public void DomainEntities_ShouldBeSealed()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("TransactionAggregation.Domain.Entities")
            .Should().BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain entities should be sealed to prevent unintended inheritance");
    }

    [Fact]
    public void ValueObjects_ShouldInheritFromValueObject()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("TransactionAggregation.Domain.Common.ValueObjects")
            .And().AreNotAbstract()
            .Should().Inherit(typeof(ValueObject))
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "All concrete value objects must inherit from ValueObject");
    }

    [Fact]
    public void DomainEvents_ShouldInheritFromBaseDomainEvent()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("TransactionAggregation.Domain.Events")
            .Should().Inherit(typeof(TransactionAggregation.Domain.Common.BaseDomainEvent))
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "All domain events must inherit from BaseDomainEvent");
    }

    [Fact]
    public void DomainEntities_ShouldResideIn_DomainLayer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().Inherit(typeof(TransactionAggregation.Domain.Common.BaseEntity))
            .Should().ResideInNamespace(DomainNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Entities must reside in the Domain layer");
    }

    [Fact]
    public void DomainExceptions_ShouldResideIn_DomainExceptionsNamespace()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().Inherit(typeof(Exception))
            .Should().ResideInNamespace("TransactionAggregation.Domain.Exceptions")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain exceptions must reside in Domain.Exceptions namespace");
    }
}
