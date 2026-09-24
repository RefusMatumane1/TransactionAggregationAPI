using Modules.BankLinks.Domain.ValueObjects;
using Modules.WebhookSources.Contracts;
using NSubstitute;

namespace TransactionAggregation.Tests.Helpers;

/// <summary>Webhook-source institution scoping for tests that aren't about that scoping.</summary>
public static class TestInstitutions
{
    public static readonly IReadOnlyList<string> All = Enum.GetNames<Institution>();

    /// <summary>A directory in which every source may deliver for every institution.</summary>
    public static IWebhookSourceDirectory AllowAllDirectory()
    {
        var directory = Substitute.For<IWebhookSourceDirectory>();
        directory.IsActiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        directory.GetAuthorizedInstitutionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>(All, StringComparer.OrdinalIgnoreCase));
        return directory;
    }
}