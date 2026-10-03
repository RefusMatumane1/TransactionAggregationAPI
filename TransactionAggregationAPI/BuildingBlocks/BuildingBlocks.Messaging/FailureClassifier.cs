using Npgsql;
using SharedKernel.Common.Enums;
using SharedKernel.Common.Models;
using SharedKernel.Exceptions;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace BuildingBlocks.Messaging
{
    public enum FailureKind
    {
        Transient,
        Permanent
    }

    // The message can never be processed as it is (undecodable, or refused by its schema).
    public sealed class PoisonMessageException(string message, Exception? innerException = null)
        : Exception(message, innerException);

    // The destination refused the message for a reason retrying cannot change (e.g. HTTP 400, 403).
    public sealed class PermanentDeliveryException(string message, Exception? innerException = null)
        : Exception(message, innerException);

    // Decides whether a failure is worth retrying. Transient failures are retried with backoff up to
    // the queue's attempt limit; permanent ones are dead-lettered at once, without spending retries.
    public static class FailureClassifier
    {
        private static readonly string[] PermanentSqlStateClasses = ["22", "23"];

        public static FailureKind Classify(Exception exception) => exception switch
        {
            PoisonMessageException or PermanentDeliveryException or JsonException or DomainException or NotSupportedException
                => FailureKind.Permanent,
            HttpRequestException { StatusCode: { } status } when IsPermanentHttpStatus(status) => FailureKind.Permanent,
            _ when FindPostgresException(exception) is { } postgres && IsPermanentSqlState(postgres.SqlState) => FailureKind.Permanent,
            _ => FailureKind.Transient
        };

        public static FailureKind Classify(Error error) => error.Type switch
        {
            ErrorType.Forbidden or ErrorType.Validation => FailureKind.Permanent,
            _ => FailureKind.Transient
        };

        public static bool IsInfrastructureOutage(Exception exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                switch (current)
                {
                    case NpgsqlException npgsql when npgsql.IsTransient:
                    case TimeoutException:
                    case SocketException:
                    case HttpRequestException:
                    case IOException:
                        return true;
                }

                if (current.GetType().FullName is "StackExchange.Redis.RedisConnectionException"
                    or "StackExchange.Redis.RedisTimeoutException")
                    return true;
            }

            return false;
        }

        public static string Describe(Exception exception)
        {
            var innermost = exception;
            while (innermost.InnerException is not null)
                innermost = innermost.InnerException;

            return innermost is PostgresException postgres
                ? $"PostgresException {postgres.SqlState}: {postgres.MessageText}"
                : $"{innermost.GetType().Name}: {innermost.Message}";
        }

        // 4xx means the request itself was refused, except timeouts and rate limiting, which pass.
        private static bool IsPermanentHttpStatus(HttpStatusCode status) =>
            (int)status is >= 400 and < 500
            && status is not HttpStatusCode.RequestTimeout and not HttpStatusCode.TooManyRequests;

        private static PostgresException? FindPostgresException(Exception exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                if (current is PostgresException postgres)
                    return postgres;
            }

            return null;
        }

        // Data and integrity violations are permanent, except a unique violation: that is a race
        // with a concurrent writer, and the retry finds the row and treats it as a duplicate.
        private static bool IsPermanentSqlState(string sqlState) =>
            sqlState != PostgresErrorCodes.UniqueViolation
            && PermanentSqlStateClasses.Any(sqlClass => sqlState.StartsWith(sqlClass, StringComparison.Ordinal));
    }
}