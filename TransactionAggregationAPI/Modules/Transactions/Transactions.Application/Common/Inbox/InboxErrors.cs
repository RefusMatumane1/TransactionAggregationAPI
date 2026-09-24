using SharedKernel.Common.Enums;
using SharedKernel.Common.Models;

namespace Modules.Transactions.Application.Common.Inbox
{
    public static class InboxErrors
    {
        public const string IdempotencyKeyReusedCode = "Inbox.IdempotencyKeyReused";

        /// <summary>
        /// 422, following the IETF Idempotency-Key draft: the request is well-formed but can
        /// never be accepted under this key, so a retry won't help (Kafka dead-letters it).
        /// </summary>
        public static Error IdempotencyKeyReused(string idempotencyKey) =>
            new(IdempotencyKeyReusedCode,
                $"Idempotency key '{idempotencyKey}' was already used for a different payload. Use a new key for new content.",
                ErrorType.Problem);
    }
}