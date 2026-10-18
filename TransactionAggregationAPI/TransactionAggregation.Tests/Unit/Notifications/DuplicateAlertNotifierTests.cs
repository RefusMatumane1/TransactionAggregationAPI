using FluentAssertions;
using Microsoft.Extensions.Options;
using Modules.Notifications.Application.Common;
using Modules.Notifications.Application.Contracts;
using Modules.Notifications.Contracts;
using Modules.Notifications.Domain;
using Modules.Notifications.Infrastructure.Persistence;
using NSubstitute;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Notifications
{
    public class DuplicateAlertNotifierTests
    {
        private const string DefaultUrl = "https://hooks.example.test/services/T000/B000/SECRET-TOKEN";
        private const string FnbUrl = "https://alerts.fnb.example.test/hooks/FNB-SECRET";
        private const string AbsaUrl = "https://alerts.absa.example.test/hooks/ABSA-SECRET";

        private sealed class ReversibleProtector : IAlertUrlProtector
        {
            public string Protect(string url) => "enc:" + url;

            public string Unprotect(string protectedUrl) => protectedUrl["enc:".Length..];
        }

        private sealed class RecordingPoster : IAlertWebhookPoster
        {
            public List<(string Url, object Payload)> Posts { get; } = [];

            public Task PostAsync(string url, object payload, CancellationToken cancellationToken = default)
            {
                Posts.Add((url, payload));
                return Task.CompletedTask;
            }
        }

        private static (DuplicateAlertNotifier Notifier, RecordingPoster Poster, RecordingLogger<DuplicateAlertNotifier> Logs) Build(
            string defaultUrl, params (string Bank, string Url)[] bankUrls)
        {
            var context = InMemoryNotificationsDbContextFactory.Create();
            var protector = new ReversibleProtector();
            foreach (var (bank, url) in bankUrls)
                context.AlertWebhooks.Add(AlertWebhook.Create(bank, protector.Protect(url)));
            context.SaveChanges();

            var poster = new RecordingPoster();
            var logs = new RecordingLogger<DuplicateAlertNotifier>();
            return (new DuplicateAlertNotifier(context, protector, poster,
                Options.Create(new NotificationOptions { DuplicateAlertWebhookUrl = defaultUrl }), logs), poster, logs);
        }

        private static DuplicateAlert From(string bank) =>
            new("transaction", bank, "1234567890", ["bank-1", "bank-2"], Guid.NewGuid(), DateTime.UtcNow);

        [Fact]
        public async Task ABanksDuplicates_GoToThatBanksOwnUrl_NotTheDefault()
        {
            var (notifier, poster, _) = Build(DefaultUrl, ("FNB", FnbUrl));

            await notifier.NotifyAsync(From("FNB"));

            poster.Posts.Should().ContainSingle().Which.Url.Should().Be(FnbUrl);
        }

        [Fact]
        public async Task EachBanksDuplicates_AreRoutedToThatBanksUrl()
        {
            var (notifier, poster, _) = Build(DefaultUrl, ("FNB", FnbUrl), ("Absa", AbsaUrl));

            await notifier.NotifyAsync(From("Absa"));
            await notifier.NotifyAsync(From("FNB"));
            await notifier.NotifyAsync(From("Capitec"));

            poster.Posts.Select(p => p.Url).Should().Equal(AbsaUrl, FnbUrl, DefaultUrl);
        }

        [Fact]
        public async Task ABankWithoutItsOwnUrl_FallsBackToTheDefault()
        {
            var (notifier, poster, logs) = Build(DefaultUrl);

            await notifier.NotifyAsync(From("FNB"));

            poster.Posts.Should().ContainSingle().Which.Url.Should().Be(DefaultUrl);
            logs.Messages.Should().Contain(m => m.Contains("alert destination: default"));
        }

        [Fact]
        public async Task WithNeitherUrl_NothingIsSent_ButTheDuplicateIsStillLogged()
        {
            var (notifier, poster, logs) = Build(string.Empty);

            await notifier.NotifyAsync(From("FNB"));

            poster.Posts.Should().BeEmpty();
            logs.Messages.Should().Contain(m => m.Contains("alert destination: none"));
        }

        [Fact]
        public async Task ThePayload_NamesTheBankAndTheDuplicates()
        {
            var (notifier, poster, _) = Build(DefaultUrl, ("FNB", FnbUrl));
            var alert = From("FNB");

            await notifier.NotifyAsync(alert);

            var json = System.Text.Json.JsonSerializer.Serialize(poster.Posts.Single().Payload);
            json.Should().Contain("\"alert_type\":\"duplicate_inbound\"").And.Contain("\"source_name\":\"FNB\"")
                .And.Contain("bank-1").And.Contain("bank-2").And.Contain(alert.InboxMessageId!.Value.ToString());
        }

        [Fact]
        public async Task NoUrl_IsEverLogged_AndTheAccountIsMasked()
        {
            var (notifier, _, logs) = Build(DefaultUrl, ("FNB", FnbUrl));

            await notifier.NotifyAsync(From("FNB"));
            await notifier.NotifyAsync(From("Capitec"));

            logs.Messages.Should().NotContain(m => m.Contains("SECRET") || m.Contains("example.test"));
            logs.Messages.Should().NotContain(m => m.Contains("1234567890"));
            logs.Messages.Should().Contain(m => m.Contains("***7890"));
            logs.Messages.Should().Contain(m => m.Contains("alert destination: bank"));
        }

        [Fact]
        public async Task WhenAUrlCannotBeDecrypted_TheAlertFails_RatherThanGoingToTheWrongPlace()
        {
            var context = InMemoryNotificationsDbContextFactory.Create();
            context.AlertWebhooks.Add(AlertWebhook.Create("FNB", "not-decryptable"));
            context.SaveChanges();
            var protector = Substitute.For<IAlertUrlProtector>();
            protector.Unprotect(Arg.Any<string>()).Returns(_ => throw new System.Security.Cryptography.CryptographicException("key ring unavailable"));
            var poster = new RecordingPoster();
            var notifier = new DuplicateAlertNotifier(context, protector, poster,
                Options.Create(new NotificationOptions { DuplicateAlertWebhookUrl = DefaultUrl }), new RecordingLogger<DuplicateAlertNotifier>());

            var act = () => notifier.NotifyAsync(From("FNB"));

            await act.Should().ThrowAsync<System.Security.Cryptography.CryptographicException>();
            poster.Posts.Should().BeEmpty("the default must not receive a bank's duplicates just because its own URL is unreadable");
        }
    }
}