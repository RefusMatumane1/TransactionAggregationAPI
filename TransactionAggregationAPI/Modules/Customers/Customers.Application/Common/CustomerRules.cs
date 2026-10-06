using BuildingBlocks.Application.Abstractions.Authentication;
using FluentValidation;
using Modules.Customers.Domain;
using SharedKernel.Common.Models;

namespace Modules.Customers.Application.Common
{
    internal static class CustomerRules
    {
        public static IRuleBuilderOptions<T, string> CustomerReference<T>(this IRuleBuilder<T, string> rule) =>
            rule.Must(Customer.IsValidReference)
                .WithMessage($"reference must be 1-{Customer.MaxReferenceLength} letters, digits, '-' or '_', starting with a letter or digit.");

        public static IRuleBuilderOptions<T, string> CustomerName<T>(this IRuleBuilder<T, string> rule) =>
            rule.Must(name => name.Trim().Length is > 0 and <= Customer.MaxNameLength)
                .WithMessage($"name must be 1-{Customer.MaxNameLength} characters.");

        public static IRuleBuilderOptions<T, string> Institution<T>(this IRuleBuilder<T, string> rule) =>
            rule.Must(Customer.IsValidInstitution)
                .WithMessage($"institution must be a bank code of 1-{Customer.MaxInstitutionLength} letters, digits, '-' or '_'.");

        public static IRuleBuilderOptions<T, string> ExternalAccountId<T>(this IRuleBuilder<T, string> rule) =>
            rule.Must(Customer.IsValidExternalAccountId)
                .WithMessage($"externalAccountId must be 1-{Customer.MaxExternalAccountIdLength} characters, without surrounding spaces.");

        // A staff member sees a customer only through accounts at the banks they are assigned; one
        // with none of those is reported exactly like a missing customer (no existence leak).
        public static bool CanSee(this InstitutionAccess access, Customer customer) =>
            access.AllInstitutions || customer.Accounts.Any(a => access.Includes(a.Institution));

        public static bool Includes(this InstitutionAccess access, string institution) =>
            access.AllInstitutions || access.Institutions.Contains(institution, StringComparer.OrdinalIgnoreCase);

        public static Error NotFound(Guid id) => Error.NotFound("Customer", id);
    }
}