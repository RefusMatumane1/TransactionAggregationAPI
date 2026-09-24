using SharedKernel.Common.Models;

namespace Modules.Customers.Application.Errors
{
    /// <summary>
    /// The single definition of "not found" for this module's resources. Handlers return it
    /// for a missing row, and endpoints return the same error for a row that exists but
    /// belongs to another customer — so the two responses are identical and can't be used
    /// to discover which ids or email addresses exist.
    /// </summary>
    public static class CustomerErrors
    {
        public static Error NotFound(Guid customerId) => Error.NotFound("Customer", customerId);

        public static Error NotFoundByEmail() =>
            new("Error.NotFound", "No customer with that email address was found.", SharedKernel.Common.Enums.ErrorType.NotFound);
    }

    /// <inheritdoc cref="CustomerErrors"/>
    public static class AccountErrors
    {
        public static Error NotFound(Guid accountId) => Error.NotFound("Account", accountId);
    }
}