using SharedKernel.Common.Enums;

namespace SharedKernel.Common.Models
{
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