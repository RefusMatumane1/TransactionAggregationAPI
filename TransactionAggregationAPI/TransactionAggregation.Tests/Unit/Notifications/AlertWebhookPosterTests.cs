using BuildingBlocks.Messaging;
using FluentAssertions;
using Modules.Notifications.Application.Common;
using NSubstitute;
using System.Net;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Notifications
{
    public class AlertWebhookPosterTests
    {
        private const string HookUrl = "https://hooks.example.test/services/T000/B000/SECRET-TOKEN";

        private static IAlertWebhookPoster Build(HttpMessageHandler handler)
        {
            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient("duplicate-alerts").Returns(_ => new HttpClient(handler));
            return new Modules.Notifications.Infrastructure.AlertWebhookPoster(factory);
        }

        [Theory]
        [InlineData(HttpStatusCode.TooManyRequests, FailureKind.Transient)]
        [InlineData(HttpStatusCode.RequestTimeout, FailureKind.Transient)]
        [InlineData(HttpStatusCode.InternalServerError, FailureKind.Transient)]
        [InlineData(HttpStatusCode.BadGateway, FailureKind.Transient)]
        [InlineData(HttpStatusCode.BadRequest, FailureKind.Permanent)]
        [InlineData(HttpStatusCode.Forbidden, FailureKind.Permanent)]
        [InlineData(HttpStatusCode.NotFound, FailureKind.Permanent)]
        public async Task AFailedPost_Throws_SoTheOutboxRetriesOrDeadLettersIt(HttpStatusCode status, FailureKind expected)
        {
            var poster = Build(new StubHandler(status));

            var act = () => poster.PostAsync(HookUrl, new { alert_type = "duplicate_inbound" });

            var thrown = (await act.Should().ThrowAsync<HttpRequestException>()).Which;
            FailureClassifier.Classify(thrown).Should().Be(expected);
        }

        [Fact]
        public async Task ATimeout_IsTransient()
        {
            var poster = Build(new StubHandler(timeout: true));

            var act = () => poster.PostAsync(HookUrl, new { alert_type = "duplicate_inbound" });

            var thrown = (await act.Should().ThrowAsync<Exception>()).Which;
            FailureClassifier.Classify(thrown).Should().Be(FailureKind.Transient);
        }

        [Fact]
        public async Task ASuccessfulPost_SendsJsonToTheGivenUrl()
        {
            var handler = new StubHandler(HttpStatusCode.OK);
            var poster = Build(handler);

            await poster.PostAsync(HookUrl, new { alert_type = "duplicate_inbound" });

            handler.Requests.Should().ContainSingle().Which.Should().Be(HookUrl);
            handler.Bodies.Single().Should().Contain("duplicate_inbound");
        }

        private sealed class StubHandler(HttpStatusCode status = HttpStatusCode.OK, bool timeout = false) : HttpMessageHandler
        {
            public List<string> Requests { get; } = [];

            public List<string> Bodies { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request.RequestUri!.ToString());
                Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
                return timeout
                    ? throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout", new TimeoutException())
                    : new HttpResponseMessage(status);
            }
        }
    }
}