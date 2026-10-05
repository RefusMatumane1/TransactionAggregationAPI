using SharedKernel.Common.Enums;
using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Common.Models
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

        public static Error Failure(string code, string message) =>
            new(code, message, ErrorType.Failure);

        public static Error NotFound(string entityName, object key) =>
            new("Error.NotFound", $"'{entityName}' with key '{key}' was not found.", ErrorType.NotFound);

        public static Error Validation(string message) =>
            new("Error.Validation", message, ErrorType.Validation);

        public static Error NotPermitted(string code, string message) =>
            new(code, message, ErrorType.Forbidden);

        public static Error Conflict(string message) =>
            new("Error.Conflict", message, ErrorType.Conflict);

    }
}