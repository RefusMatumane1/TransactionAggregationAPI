namespace SharedKernel.Exceptions
{
    public class DomainException : Exception
    {
        public DomainException()
            : base("A domain rule was violated")
        {
        }

        public DomainException(string message)
            : base(message)
        {
        }

        public DomainException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
