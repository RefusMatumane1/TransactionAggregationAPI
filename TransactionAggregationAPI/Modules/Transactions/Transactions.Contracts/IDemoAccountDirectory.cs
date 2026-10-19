namespace Modules.Transactions.Contracts
{
    public sealed record DemoAccount(string Institution, string ExternalAccountId);

    /// <summary>The demo bank accounts the Development seed creates, grouped by the household that owns them.</summary>
    public interface IDemoAccountDirectory
    {
        IReadOnlyList<IReadOnlyList<DemoAccount>> Households { get; }
    }
}
