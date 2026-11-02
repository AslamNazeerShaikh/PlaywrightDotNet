namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Waits between requests. Policies decide how long; sleeping belongs to
    /// infrastructure (a <see cref="TimeProvider"/>-based delay), so tests
    /// substitute a fake that records instead of sleeping.
    /// </summary>
    public interface IDelay
    {
        /// <summary>
        /// Waits for <paramref name="duration"/>.
        /// </summary>
        /// <param name="duration">How long to wait; non-negative.</param>
        /// <param name="cancellationToken">Token to observe; cancels the wait.</param>
        /// <returns>A task that completes after the wait.</returns>
        Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken);
    }
}
