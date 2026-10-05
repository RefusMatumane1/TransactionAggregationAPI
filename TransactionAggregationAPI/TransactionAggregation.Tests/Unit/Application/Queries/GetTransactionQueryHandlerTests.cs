using BuildingBlocks.Application.Abstractions.Authentication;
using FluentAssertions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransaction;
using SharedKernel.Common.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries
{
    public class GetTransactionQueryHandlerTests
    {
        [Fact]
        public async Task Handle_ExistingTransaction_ReturnsMappedDto()
        {
            var context = InMemoryDbContextFactory.Create();
            var tx = TestTransactions.Create(-250m, "uber ride", institution: "BogusBank");
            context.Transactions.Add(tx);
            await context.SaveChangesAsync();

            var result = await new GetTransactionQueryHandler(context).Handle(
                new GetTransactionQuery(tx.Id.Value, InstitutionAccess.All), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(tx.Id.Value);
            result.Value.Amount.Should().Be(-250m);
            result.Value.Description.Should().Be("uber ride");
            result.Value.Institution.Should().Be("BogusBank");
            result.Value.ExternalAccountId.Should().Be(tx.ExternalAccountId);
        }

        [Fact]
        public async Task Handle_NonExistentTransaction_ReturnsNotFound()
        {
            var context = InMemoryDbContextFactory.Create();

            var result = await new GetTransactionQueryHandler(context).Handle(
                new GetTransactionQuery(Guid.NewGuid(), InstitutionAccess.All), CancellationToken.None);

            result.Error.Type.Should().Be(ErrorType.NotFound);
        }

        [Fact]
        public async Task Handle_TransactionOfAnInstitutionTheCallerMayNotRead_IsNotFound_NotForbidden()
        {
            var context = InMemoryDbContextFactory.Create();
            var tx = TestTransactions.Create(-250m, institution: TestInstitutions.Capitec);
            context.Transactions.Add(tx);
            await context.SaveChangesAsync();

            var result = await new GetTransactionQueryHandler(context).Handle(
                new GetTransactionQuery(tx.Id.Value, InstitutionAccess.Only([TestInstitutions.FNB])), CancellationToken.None);

            result.Error.Type.Should().Be(ErrorType.NotFound, "a 403 would confirm the id exists");
        }
    }
}