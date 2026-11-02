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
    }
}
