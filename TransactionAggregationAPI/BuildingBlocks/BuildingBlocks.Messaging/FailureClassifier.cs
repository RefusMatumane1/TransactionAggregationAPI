using SharedKernel.Common.Enums;
using SharedKernel.Common.Models;
using SharedKernel.Exceptions;
using System.Text.Json;

namespace BuildingBlocks.Messaging
{
    /// <summary>
    /// Whether retrying a failed inbox/outbox message could ever succeed. Transient failures
    /// (database, broker, network, timeouts, and anything unrecognised) get the bounded
    /// retry-with-backoff budget; permanent ones are dead-lettered at once, because spending
    /// the budget on them only delays the operator alert and burns capacity.
    /// </summary>
    public enum FailureKind
    {
        Transient,
        Permanent
    }

    /// <summary>A message whose content can never be processed (undecodable, empty, wrong shape).</summary>
    public sealed class PoisonMessageException(string message, Exception? innerException = null)
        : Exception(message, innerException);

    public static class FailureClassifier
    {
        public static FailureKind Classify(Exception exception) => exception switch
        {
            PoisonMessageException or JsonException or DomainException or NotSupportedException => FailureKind.Permanent,
            _ => FailureKind.Transient
        };

        /// <summary>
        /// A refused or invalid request stays refused however often it's resent; not-found and
        /// conflict can resolve themselves (a bank link activated later, a racing writer
        /// finishing), so they stay transient.
        /// </summary>
        public static FailureKind Classify(Error error) => error.Type switch
        {
            ErrorType.Forbidden or ErrorType.Validation => FailureKind.Permanent,
            _ => FailureKind.Transient
        };
    }
}