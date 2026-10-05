using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace TransactionAggregation.Tests.Architecture
{
    public class PresentationLayerTests
    {
        private static readonly string[] Modules = ["Audit", "Transactions", "WebhookSources"];

        public static TheoryData<string> ModuleNames => new(Modules);

        private static Assembly Load(string module, string layer) => Assembly.Load(new AssemblyName($"{module}.{layer}"));

        private static string[] InnerLayersOf(string module) =>
            module == "Transactions"
                ? ["Domain", "Application", "Infrastructure"]
                : ["Domain", "Application", "Infrastructure", "Contracts"];

        [Theory]
        [MemberData(nameof(ModuleNames))]
        public void InnerLayers_ShouldNotReference_AnyPresentationAssembly(string module)
        {
            foreach (var layer in InnerLayersOf(module))
            {
                var references = Load(module, layer).GetReferencedAssemblies().Select(a => a.Name);

                references.Should().NotContain(name => name!.EndsWith(".Presentation"),
                    because: $"{module}.{layer} is below Presentation — only the API host may reference a module's HTTP surface");
            }
        }

        [Theory]
        [MemberData(nameof(ModuleNames))]
        public void Presentation_ShouldNotDependOn_InfrastructureOrPersistence(string module)
        {
            var result = Types.InAssembly(Load(module, "Presentation"))
                .Should()
                .NotHaveDependencyOnAny([.. Modules.Select(m => $"Modules.{m}.Infrastructure"), "Microsoft.EntityFrameworkCore", "Npgsql"])
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: "endpoints translate HTTP to commands/queries and results back to responses — data access belongs behind Application");
        }

        [Theory]
        [MemberData(nameof(ModuleNames))]
        public void Presentation_ShouldNotDependOn_OtherModulesInternals(string module)
        {
            var otherModulesInternals = Modules
                .Where(m => m != module)
                .SelectMany(m => new[] { $"Modules.{m}.Domain", $"Modules.{m}.Application", $"Modules.{m}.Presentation" })
                .ToArray();

            var result = Types.InAssembly(Load(module, "Presentation"))
                .Should()
                .NotHaveDependencyOnAny(otherModulesInternals)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: "a module's endpoints may reach another module only through its published *.Contracts");
        }

        [Theory]
        [MemberData(nameof(ModuleNames))]
        public void Infrastructure_ShouldNotMapEndpoints(string module)
        {
            var result = Types.InAssembly(Load(module, "Infrastructure"))
                .Should()
                .NotHaveDependencyOnAny("Microsoft.AspNetCore.Routing", "Microsoft.AspNetCore.Http.IResult", "BuildingBlocks.Web")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: "HTTP endpoints belong in the module's Presentation project, not in Infrastructure");
        }

        [Theory]
        [MemberData(nameof(ModuleNames))]
        public void EndpointHandlers_ShouldNotBePublic(string module)
        {
            var result = Types.InAssembly(Load(module, "Presentation"))
                .That().ResideInNamespaceContaining(".Presentation.Endpoints")
                .Should().NotBePublic()
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: "a module's public HTTP surface is its route table (Map*Endpoints) and its request/response contracts — individual handlers are implementation details");
        }
    }
}