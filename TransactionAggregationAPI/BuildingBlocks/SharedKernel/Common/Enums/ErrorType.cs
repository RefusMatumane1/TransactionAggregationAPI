namespace SharedKernel.Common.Enums
{
    public enum ErrorType
    {
        Failure = 0,
        Validation = 1,
        Problem = 2,
        NotFound = 3,
        Conflict = 4,

        /// <summary>The caller is identified but not permitted to act on this resource.</summary>
        Forbidden = 5
    }
}