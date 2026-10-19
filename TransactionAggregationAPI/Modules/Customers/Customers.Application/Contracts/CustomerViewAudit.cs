using BuildingBlocks.Application.Abstractions.Authentication;
using Modules.Audit.Contracts;
using Modules.Customers.Contracts;

namespace Modules.Customers.Application.Contracts
{
    internal sealed class CustomerViewAudit(IAuditTrail auditTrail, IUserContext user) : ICustomerViewAudit
    {
        public string RouteKey => "customerId";

        public Task RecordAsync(Guid customerId, string view, CancellationToken cancellationToken)
        {
            var access = user.InstitutionAccess;
            var scope = access.AllInstitutions ? "all" : string.Join(',', access.Institutions);

            return auditTrail.RecordAsync([AccessAudit.CustomerViewed(user.UserId, customerId, view, scope)], cancellationToken);
        }
    }
}
