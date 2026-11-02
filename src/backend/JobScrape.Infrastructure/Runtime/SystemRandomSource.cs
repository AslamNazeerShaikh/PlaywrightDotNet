using JobScrape.Domain.Abstractions;

namespace JobScrape.Infrastructure.Runtime
{
    /// <summary>
    /// <see cref="IRandomSource"/> over the thread-safe <see cref="Random.Shared"/>.
    /// Jitter only; never used for anything security-sensitive.
    /// </summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        /// <summary>
        /// Returns a uniform integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).
        /// </summary>
        /// <param name="minInclusive">The inclusive lower bound.</param>
        /// <param name="maxExclusive">The exclusive upper bound.</param>
        /// <returns>The random integer.</returns>
        public int NextInt32(int minInclusive, int maxExclusive)
        {
            return Random.Shared.Next(minInclusive, maxExclusive);
        }
    }
}
