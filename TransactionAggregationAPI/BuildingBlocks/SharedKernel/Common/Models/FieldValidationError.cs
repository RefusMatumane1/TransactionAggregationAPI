using SharedKernel.Common.Enums;

namespace SharedKernel.Common.Models
{
    /// <summary>
    /// A validation failure that keeps each message against the field it's about, so API
    /// clients get RFC 9457 "errors": { field: [messages] } rather than one joined string.
    /// <see cref="Error.Description"/> still carries the joined text for logs and audit.
    /// </summary>
    public sealed record FieldValidationError : Error
    {
        public FieldValidationError(IReadOnlyDictionary<string, string[]> errors)
            : base(
                "Error.Validation",
                string.Join("; ", errors.SelectMany(e => e.Value)),
                ErrorType.Validation)
        {
            Errors = errors;
        }

        public IReadOnlyDictionary<string, string[]> Errors { get; }
    }
}