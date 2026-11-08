using JobScrape.Domain.Access;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Read-write port for <see cref="UsageQuota"/> definitions, keyed by
    /// scope. Definitions persist across restarts; enforcement counters do
    /// not and live at the API edge instead.
    /// </summary>
    public interface IUsageQuotaRepository
    {
        /// <summary>
        /// Loads one scope's definition, or null when absent.
        /// </summary>
        /// <param name="scope">The governed surface.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The definition, or null.</returns>
        Task<UsageQuota?> GetAsync(QuotaScope scope, CancellationToken cancellationToken);

        /// <summary>
        /// Lists every stored definition, in scope order.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>All definitions.</returns>
        Task<IReadOnlyList<UsageQuota>> ListAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Stages a new definition; persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.
        /// </summary>
        /// <param name="quota">The definition.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the definition is staged.</returns>
        Task AddAsync(UsageQuota quota, CancellationToken cancellationToken);
    }
}
