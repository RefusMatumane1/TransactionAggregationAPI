using Microsoft.EntityFrameworkCore;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Contracts;
using SharedKernel.Common.ValueObjects;

namespace Modules.Customers.Application.Contracts
{
    internal sealed class CustomersReadApi(ICustomersDbContext context) : ICustomersReadApi
    {
        public async Task<CustomerSummary?> FindCustomerByIdAsync(
            Guid customerId,
            CancellationToken cancellationToken = default)
        {
            var customerIdVo = CustomerId.CreateFrom(customerId);

            var customer = await context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerIdVo, cancellationToken);

            if (customer is null)
                return null;

            return new CustomerSummary(
                customer.Id.Value,
                customer.Email,
                customer.Name,
                customer.CreatedAt,
                customer.UpdatedAt);
        }
    }
}