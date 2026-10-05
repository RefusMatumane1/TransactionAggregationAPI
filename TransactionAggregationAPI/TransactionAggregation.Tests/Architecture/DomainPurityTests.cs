using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace TransactionAggregation.Tests.Architecture
{
    public class DomainPurityTests
    {
        private static readonly string[] FrameworkNamespaces =
        [
            "Microsoft.EntityFrameworkCore", "Npgsql", "FluentValidation", "Serilog", "Microsoft.AspNetCore"
        ];

        public static TheoryData<string> DomainAssemblies => new()
        {
            typeof(SharedKernel.Common.BaseEntity).Assembly.GetName().Name!,
            typeof(Modules.Transactions.Domain.Entities.Transaction).Assembly.GetName().Name!,
            typeof(Modules.WebhookSources.Domain.WebhookSource).Assembly.GetName().Name!,
            typeof(Modules.Audit.Domain.AuditEvent).Assembly.GetName().Name!
        };

        [Theory]
        [MemberData(nameof(DomainAssemblies))]
        public void DomainAssembly_DependsOnNoPersistenceValidationOrLoggingFramework(string assemblyName)
        {
            var result = Types.InAssembly(Assembly.Load(assemblyName))
                .Should().NotHaveDependencyOnAny(FrameworkNamespaces)
                .GetResult();

            result.FailingTypeNames.Should().BeNullOrEmpty(
                because: $"{assemblyName} is a domain assembly and must stay framework-free");
        }

        [Theory]
        [MemberData(nameof(DomainAssemblies))]
        public void DomainAssembly_DoesNotReferenceFrameworkAssemblies(string assemblyName)
        {
            var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name!);

            references.Should().NotContain(r => FrameworkNamespaces.Any(f => r.StartsWith(f, StringComparison.Ordinal)));
        }
    }
}