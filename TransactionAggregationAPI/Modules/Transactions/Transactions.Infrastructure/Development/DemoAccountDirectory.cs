using Modules.Transactions.Contracts;

namespace Modules.Transactions.Infrastructure.Development
{
    internal sealed class DemoAccountDirectory : IDemoAccountDirectory
    {
        public IReadOnlyList<IReadOnlyList<DemoAccount>> Households { get; } =
        [
            .. SeedCatalog.Households.Select(h =>
                (IReadOnlyList<DemoAccount>)[.. h.Accounts.Select(a => new DemoAccount(a.Institution, a.ExternalAccountId))])
        ];
    }
}
