using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    public class CompositionTests(IntegrationTestWebAppFactory factory) : IClassFixture<IntegrationTestWebAppFactory>
    {
        [Fact]
        public void Validators_AreRegisteredOnce()
        {
            using var scope = factory.Services.CreateScope();

            scope.ServiceProvider.GetServices<IValidator<ReceiveBankTransactionsCommand>>()
                .Should().ContainSingle();
        }

        [Fact]
        public void PipelineBehaviors_AreRegisteredOnce()
        {
            using var scope = factory.Services.CreateScope();

            var behaviors = scope.ServiceProvider
                .GetServices<IPipelineBehavior<ReceiveBankTransactionsCommand, SharedKernel.Common.Models.Result<InboxReceipt>>>()
                .Select(b => b.GetType().GetGenericTypeDefinition())
                .ToList();

            behaviors.Should().OnlyHaveUniqueItems();
        }
    }
}