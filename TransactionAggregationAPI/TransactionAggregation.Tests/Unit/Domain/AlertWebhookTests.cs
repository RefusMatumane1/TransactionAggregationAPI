using FluentAssertions;
using Modules.Notifications.Domain;
using SharedKernel.Exceptions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain
{
    public class AlertWebhookTests
    {
        [Theory]
        [InlineData("https://hooks.example.com/services/T0/B0/secret", true)]
        [InlineData("https://hooks.example.com:8443/a?token=abc", true)]
        [InlineData("https://8.8.8.8/hook", true)]
        [InlineData("http://hooks.example.com/hook", false)]
        [InlineData("https://user:pass@hooks.example.com/hook", false)]
        [InlineData("https://localhost/hook", false)]
        [InlineData("https://alerts.localhost/hook", false)]
        [InlineData("https://intranet/hook", false)]
        [InlineData("https://alerts.corp.internal/hook", false)]
        [InlineData("https://printer.local/hook", false)]
        [InlineData("https://127.0.0.1/hook", false)]
        [InlineData("https://10.0.0.5/hook", false)]
        [InlineData("https://172.16.4.1/hook", false)]
        [InlineData("https://192.168.1.10/hook", false)]
        [InlineData("https://169.254.169.254/latest/meta-data", false)]
        [InlineData("https://100.64.0.1/hook", false)]
        [InlineData("https://[::1]/hook", false)]
        [InlineData("https://[fd00::1]/hook", false)]
        [InlineData("https://[::ffff:10.0.0.1]/hook", false)]
        [InlineData("ftp://hooks.example.com/hook", false)]
        [InlineData("not a url", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidUrl_AcceptsOnlyHttpsUrlsToPublicHosts(string? url, bool expected) =>
            AlertWebhook.IsValidUrl(url).Should().Be(expected);

        [Fact]
        public void IsValidUrl_RejectsAnOverlongUrl() =>
            AlertWebhook.IsValidUrl("https://hooks.example.com/" + new string('a', AlertWebhook.MaximumUrlLength)).Should().BeFalse();

        [Fact]
        public void Create_And_Replace_HoldOnlyTheProtectedValue()
        {
            var webhook = AlertWebhook.Create("FNB", "protected-1");
            webhook.ProtectedUrl.Should().Be("protected-1");

            webhook.Replace("protected-2");

            webhook.ProtectedUrl.Should().Be("protected-2");
            webhook.BankCode.Should().Be("FNB");
        }

        [Theory]
        [InlineData("", "protected")]
        [InlineData("FNB", "")]
        public void Create_RejectsAnEmptyBankOrValue(string bank, string value)
        {
            var act = () => AlertWebhook.Create(bank, value);

            act.Should().Throw<DomainException>();
        }
    }
}