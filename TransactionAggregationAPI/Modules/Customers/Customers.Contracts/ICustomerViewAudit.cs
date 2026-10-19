using SharedKernel.Common.Auditing;

namespace Modules.Customers.Contracts
{
    /// <summary>Audits a view of one customer's data. Wire it onto any <c>/customers/{customerId}/…</c> endpoint with <c>ViewAuditFilter</c>.</summary>
    public interface ICustomerViewAudit : IViewAudit;
}
