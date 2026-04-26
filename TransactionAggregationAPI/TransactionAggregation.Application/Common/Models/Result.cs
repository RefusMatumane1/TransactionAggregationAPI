using System.Diagnostics.CodeAnalysis;
using TransactionAggregation.Application.Common.Enums;

namespace TransactionAggregation.Application.Common.Models
{
    public class Result
    {
        public Result(bool isSuccess, Error error)
        {
            if (isSuccess && error != Error.None ||
                !isSuccess && error == Error.None)
            {
                throw new ArgumentException("Invalid error", nameof(error));
            }

            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;


        public Error Error { get; }

        public static Result Success() => new(true, Error.None);

        public static Result<TValue> Success<TValue>(TValue value) =>
            new(value, true, Error.None);

        public static Result Failure(Error error) => new(false, error);

        public static Result<TValue> Failure<TValue>(Error error) =>
            new(default, false, error);
    }

    public class Result<TValue> : Result
    {
        private readonly TValue? _value;

        public Result(TValue? value, bool isSuccess, Error error)
            : base(isSuccess, error)
        {
            _value = value;
        }

        [NotNull]
        public TValue Value => IsSuccess
            ? _value!
            : throw new InvalidOperationException("The value of a failure result can't be accessed.");

        public static implicit operator Result<TValue>(TValue? value) =>
            value is not null ? Success(value) : Failure<TValue>(Error.NullValue);

        public static Result<TValue> ValidationFailure(Error error) =>
            new(default, false, error);
    }


    public record Error
    {
        public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

        public static readonly Error NullValue = new(
            "General.Null",
            "Null value was provided",
            ErrorType.Failure);

        public Error(string code, string description, ErrorType type)
        {
            Code = code;
            Description = description;
            Type = type;
        }
        public string Code { get; }

        public string Description { get; }

        public ErrorType Type { get; }


        public static readonly Error InvalidOperation =
            new("Error.InvalidOperation", "The operation is invalid.", ErrorType.Problem);

        public static readonly Error Unauthorized =
            new("Error.Unauthorized", "You are not authorized to perform this action.", ErrorType.Problem);

        public static readonly Error Forbidden =
            new("Error.Forbidden", "You do not have permission to access this resource.", ErrorType.Problem);

        public static readonly Error Timeout =
            new("Error.Timeout", "The operation has timed out.", ErrorType.Problem);

        public static readonly Error Unexpected =
            new("Error.Unexpected", "An unexpected error occurred.", ErrorType.Problem);

        public static Error Failure(string code, string message) =>
            new(code, message, ErrorType.Failure);

        public static Error NotFound(string entityName, object key) =>
            new("Error.NotFound", $"'{entityName}' with key '{key}' was not found.", ErrorType.NotFound);

        public static Error Validation(string message) =>
            new("Error.Validation", message, ErrorType.Validation);

        public static Error Conflict(string message) =>
            new("Error.Conflict", message, ErrorType.Conflict);

        public static Error Duplicate(string entityName, object value) =>
            new("Error.Duplicate", $"'{entityName}' with value '{value}' already exists.", ErrorType.Conflict);

        public static Error Required(string fieldName) =>
            new("Error.Required", $"The field '{fieldName}' is required.", ErrorType.Validation);

        public static Error Invalid(string fieldName) =>
            new("Error.Invalid", $"The field '{fieldName}' is invalid.", ErrorType.Validation);
    }
}
