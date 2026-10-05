using FluentAssertions;
using Modules.Transactions.Domain.Entities;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace TransactionAggregation.Tests.Architecture
{
    public class LayerDependencyTests
    {
        private static readonly Assembly DomainAssembly = typeof(Transaction).Assembly;
        private static readonly Assembly ApplicationAssembly = typeof(Modules.Transactions.TransactionsApplicationDependencyInjection).Assembly;

        private const string DomainNs = "Modules.Transactions.Domain";
        private const string ApplicationNs = "Modules.Transactions.Application";
        private const string InfrastructureNs = "Modules.Transactions.Infrastructure";

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
        public void Application_ShouldNot_DependOn_Infrastructure()
        {
            var result = Types.InAssembly(ApplicationAssembly)
                .Should().NotHaveDependencyOn(InfrastructureNs)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: "Application must not depend on Infrastructure; use interfaces instead");
        }

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
                .That().ResideInNamespace("Modules.Transactions.Application.Common.Interfaces")
                .And().AreInterfaces()
                .Should().HaveNameStartingWith("I")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: "All interfaces in Application.Common.Interfaces must follow the I-prefix convention");
        }

        [Fact]
        public void DomainEntities_ShouldBeSealed()
        {
            var result = Types.InAssembly(DomainAssembly)
                .That().ResideInNamespace("Modules.Transactions.Domain.Entities")
                .Should().BeSealed()
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: "Domain entities should be sealed to prevent unintended inheritance");
        }

        [Fact]
        public void ValueObjects_ShouldInheritFromValueObject()
        {
            var result = Types.InAssembly(DomainAssembly)
                .That().ResideInNamespace("Modules.Transactions.Domain.Common.ValueObjects")
                .And().AreNotAbstract()
                .Should().Inherit(typeof(SharedKernel.Common.ValueObjects.ValueObject))
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: "All concrete value objects must inherit from ValueObject");
        }

        [Fact]
        public void Domain_AndSharedKernel_ShouldNot_DependOn_MediatR()
        {
            var result = Types.InAssemblies([DomainAssembly, typeof(SharedKernel.Common.BaseEntity).Assembly])
                .Should().NotHaveDependencyOn("MediatR")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: "entities raise no events; messaging is the Application layer's concern");
        }

        [Fact]
        public void DomainEntities_ShouldResideIn_DomainLayer()
        {
            var result = Types.InAssembly(DomainAssembly)
                .That().Inherit(typeof(SharedKernel.Common.BaseEntity))
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
                .Should().ResideInNamespace("Modules.Transactions.Domain.Exceptions")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: "Domain exceptions must reside in Domain.Exceptions namespace");
        }
    }
}