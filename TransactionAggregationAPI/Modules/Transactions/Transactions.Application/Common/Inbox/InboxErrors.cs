using SharedKernel.Common.Enums;
using SharedKernel.Common.Models;

namespace Modules.Transactions.Application.Common.Inbox
{
    public static class InboxErrors
    {
        public const string IdempotencyKeyReusedCode = "Inbox.IdempotencyKeyReused";

        public static Error IdempotencyKeyReused(string idempotencyKey) =>
            new(IdempotencyKeyReusedCode,
                $"Idempotency key '{idempotencyKey}' was already used for a different payload. Use a new key for new content.",
                ErrorType.Problem);

        public static Error InstitutionNotTheSourcesBank(string sourceName, string institution) =>
            new FieldValidationError(new Dictionary<string, string[]>
            {
                ["institution"] = [$"This key belongs to bank '{sourceName}', but the delivery names '{institution}'. Omit institution or send '{sourceName}'."]
            });
    }
}