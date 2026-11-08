namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Commits every staged and changed aggregate atomically. Handlers save
    /// after each page so a crash or cancellation keeps partial results.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Persists all pending changes.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The number of written rows.</returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Reports whether <paramref name="exception"/> is a SQLite unique
        /// constraint violation, walking its inner exception chain. Used to
        /// distinguish a lost insert race from foreign-key or NOT NULL
        /// failures that must propagate.
        /// </summary>
        /// <param name="exception">The exception to inspect.</param>
        /// <returns>True when it is a unique violation.</returns>
        bool IsUniqueViolation(Exception exception);
    }
}
