namespace JobScrape.Domain
{
    /// <summary>
    /// Thrown when a domain invariant is violated. Invariant violations are
    /// programmer errors, never expected operational outcomes: handlers return
    /// a <c>Result</c> for those instead of throwing.
    /// </summary>
    public sealed class DomainException : Exception
    {
        public DomainException(string message)
            : base(message) { }

        public DomainException(string message, Exception inner)
            : base(message, inner) { }
    }
}
