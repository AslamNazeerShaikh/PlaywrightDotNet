namespace JobScrape.Domain
{
    /// <summary>
    /// Thrown when a domain invariant is violated. Invariant violations are
    /// programmer errors, never expected operational outcomes: handlers return
    /// a <c>Result</c> for those instead of throwing.
    /// </summary>
    public sealed class DomainException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DomainException"/>
        /// class with the specified message describing the violated invariant.
        /// </summary>
        /// <param name="message">The message describing the violated invariant.</param>
        public DomainException(string message)
            : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainException"/>
        /// class with the specified message and the exception that caused it.
        /// </summary>
        /// <param name="message">The message describing the violated invariant.</param>
        /// <param name="inner">The exception that caused this exception.</param>
        public DomainException(string message, Exception inner)
            : base(message, inner) { }
    }
}
