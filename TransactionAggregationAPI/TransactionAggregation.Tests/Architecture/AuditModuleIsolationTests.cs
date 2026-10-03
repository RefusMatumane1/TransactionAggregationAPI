using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace TransactionAggregation.Tests.Architecture
{
    public class AuditModuleIsolationTests
    {
        private static readonly Assembly AuditContractsAssembly = typeof(Modules.Audit.Contracts.IAuditTrail).Assembly;
        private static readonly Assembly AuditDomainAssembly = typeof(Modules.Audit.Domain.AuditEvent).Assembly;
        private static readonly Assembly AuditApplicationAssembly = typeof(Modules.Audit.AssemblyReference).Assembly;
        private static readonly Assembly AuditInfrastructureAssembly = typeof(Modules.Audit.Infrastructure.Persistence.AuditDbContext).Assembly;

        private static readonly Assembly TransactionsApplicationAssembly =
            typeof(Modules.Transactions.Application.Common.Audit.InboundAudit).Assembly;
        private static readonly Assembly TransactionsInfrastructureAssembly =
            typeof(Modules.Transactions.Infrastructure.Persistence.TransactionsDbContext).Assembly;

        private static readonly string[] OtherModules =
        [
            "Modules.Transactions",
            "Modules.WebhookSources",
            "BuildingBlocks.Messaging"
        ];

        public static TheoryData<string> AuditAssemblies => new()
        {
            nameof(AuditContractsAssembly),
            nameof(AuditDomainAssembly),
            nameof(AuditApplicationAssembly),
            nameof(AuditInfrastructureAssembly)
        };

        private static Assembly Resolve(string name) => name switch
        {
            nameof(AuditContractsAssembly) => AuditContractsAssembly,
            nameof(AuditDomainAssembly) => AuditDomainAssembly,
            nameof(AuditApplicationAssembly) => AuditApplicationAssembly,
            _ => AuditInfrastructureAssembly
        };

        [Theory]
        [MemberData(nameof(AuditAssemblies))]
        public void AuditModule_ShouldNotDependOn_AnyOtherBusinessModule(string assemblyName)
        {
            var result = Types.InAssembly(Resolve(assemblyName))
                .Should()
                .NotHaveDependencyOnAny(OtherModules)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: $"{assemblyName} must not depend on the modules it audits — they report to it through Audit.Contracts, never the other way round");
        }

        [Fact]
        public void AuditContracts_ShouldNotDependOn_AuditInternals()
        {
            var result = Types.InAssembly(AuditContractsAssembly)
                .Should()
                .NotHaveDependencyOnAny("Modules.Audit.Domain", "Modules.Audit.Application", "Modules.Audit.Infrastructure")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(because: "Contracts is the leaf other modules reference — it can't pull in the module's implementation");
        }

        [Fact]
        public void AuditDomain_ShouldNotDependOn_ApplicationOrInfrastructure()
        {
            var result = Types.InAssembly(AuditDomainAssembly)
                .Should()
                .NotHaveDependencyOnAny("Modules.Audit.Application", "Modules.Audit.Infrastructure")
                .GetResult();

            result.IsSuccessful.Should().BeTrue();
        }

        [Fact]
        public void AuditApplication_ShouldNotDependOn_Infrastructure()
        {
            var result = Types.InAssembly(AuditApplicationAssembly)
                .Should()
                .NotHaveDependencyOnAny("Modules.Audit.Infrastructure")
                .GetResult();

            result.IsSuccessful.Should().BeTrue();
        }

        [Fact]
        public void TransactionsModule_ShouldReachAudit_OnlyThroughContracts()
        {
            var result = Types.InAssemblies([TransactionsApplicationAssembly, TransactionsInfrastructureAssembly])
                .Should()
                .NotHaveDependencyOnAny("Modules.Audit.Domain", "Modules.Audit.Application", "Modules.Audit.Infrastructure")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: "producers write audit records through IAuditTrail / AuditOutbox in Audit.Contracts, never Audit's tables or handlers directly");
        }
    }
}