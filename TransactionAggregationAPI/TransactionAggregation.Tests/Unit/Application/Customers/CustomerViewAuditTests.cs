using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Audit.Contracts;
using Modules.Customers.Application.Contracts;
using Modules.Customers.Contracts;
using NSubstitute;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Customers
{
    public class CustomerViewAuditTests
    {
        private static readonly Guid Staff = Guid.NewGuid();

        private static CustomerViewAudit Audit(RecordingAuditTrail trail, InstitutionAccess access)
        {
            var user = Substitute.For<IUserContext>();
            user.UserId.Returns(Staff);
            user.InstitutionAccess.Returns(access);
            return new CustomerViewAudit(trail, user);
        }

        [Fact]
        public async Task AView_IsRecordedAgainstTheStaffMember_WithWhatTheyWereAllowedToSee()
        {
            var trail = new RecordingAuditTrail();
            var customer = Guid.NewGuid();

            await Audit(trail, InstitutionAccess.Only(["FNB", "Capitec"])).RecordAsync(customer, "GetCustomer", CancellationToken.None);

            var recorded = trail.Recorded.Should().ContainSingle().Subject;
            recorded.EventType.Should().Be(AuditEventTypes.CustomerViewed);
            recorded.Actor.Should().Be(Staff.ToString());
            recorded.Metadata!["customerId"].Should().Be(customer.ToString());
            recorded.Metadata["view"].Should().Be("GetCustomer");
            recorded.Metadata["institutionScope"].Should().Be("Capitec,FNB");
        }

        [Fact]
        public async Task AnAdministratorsView_IsRecordedAsAllBanks()
        {
            var trail = new RecordingAuditTrail();

            await Audit(trail, InstitutionAccess.All).RecordAsync(Guid.NewGuid(), "GetCustomer", CancellationToken.None);

            trail.Recorded.Single().Metadata!["institutionScope"].Should().Be("all");
        }

        [Fact]
        public void TheCustomerIdIsFoundInTheCustomerIdRouteValue() =>
            Audit(new RecordingAuditTrail(), InstitutionAccess.All).RouteKey.Should().Be("customerId");
    }

    public class ViewAuditFilterTests
    {
        private static (ViewAuditFilter<ICustomerViewAudit> Filter, ICustomerViewAudit Audit) Filter()
        {
            var audit = Substitute.For<ICustomerViewAudit>();
            audit.RouteKey.Returns("customerId");
            return (new ViewAuditFilter<ICustomerViewAudit>(audit), audit);
        }

        private static DefaultHttpContext Request(string? customerId, string? endpointName = "GetCustomer", CancellationToken aborted = default)
        {
            var http = new DefaultHttpContext { RequestAborted = aborted };
            http.Request.Path = "/api/v1/customers/anything";
            if (customerId is not null)
                http.Request.RouteValues["customerId"] = customerId;
            if (endpointName is not null)
                http.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new EndpointNameMetadata(endpointName)), "test"));
            return http;
        }

        private static ValueTask<object?> RunAsync(ViewAuditFilter<ICustomerViewAudit> filter, HttpContext http, object? result) =>
            filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(http), _ => ValueTask.FromResult(result));

        [Fact]
        public async Task ASuccessfulRead_IsAudited_AndTheResponseIsPassedThrough()
        {
            var (filter, audit) = Filter();
            var customer = Guid.NewGuid();
            var response = TypedResults.Ok();

            var returned = await RunAsync(filter, Request(customer.ToString()), response);

            returned.Should().BeSameAs(response);
            await audit.Received(1).RecordAsync(customer, "GetCustomer", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task AReadThatFailed_IsNotAudited_BecauseNothingWasShown()
        {
            var (filter, audit) = Filter();

            await RunAsync(filter, Request(Guid.NewGuid().ToString()), TypedResults.NotFound());

            await audit.DidNotReceiveWithAnyArgs().RecordAsync(default, default!, default);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not-a-guid")]
        public async Task ARouteWithoutACustomerId_IsNotAudited(string? customerId)
        {
            var (filter, audit) = Filter();

            await RunAsync(filter, Request(customerId), TypedResults.Ok());

            await audit.DidNotReceiveWithAnyArgs().RecordAsync(default, default!, default);
        }

        [Fact]
        public async Task TheViewIsNamedAfterTheEndpoint_OrTheRequestPathWhenItHasNone()
        {
            var (filter, audit) = Filter();
            var customer = Guid.NewGuid();

            await RunAsync(filter, Request(customer.ToString(), endpointName: null), TypedResults.Ok());

            await audit.Received(1).RecordAsync(customer, "/api/v1/customers/anything", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ThePersistedAuditRow_DoesNotDependOnTheClientStayingConnected()
        {
            var (filter, audit) = Filter();
            using var disconnected = new CancellationTokenSource();
            await disconnected.CancelAsync();

            await RunAsync(filter, Request(Guid.NewGuid().ToString(), aborted: disconnected.Token), TypedResults.Ok());

            await audit.Received(1).RecordAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Is<CancellationToken>(t => !t.CanBeCanceled));
        }
    }
}
