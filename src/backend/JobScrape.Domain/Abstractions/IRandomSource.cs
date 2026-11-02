namespace JobScrape.Domain.Abstractions
{
    /// <summary>
    /// Injected randomness for jitter. Domain code never calls
    /// <c>new Random()</c>; tests substitute a deterministic fake.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>
        /// Returns a uniformly distributed integer in
        /// [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).
        /// </summary>
        /// <param name="minInclusive">The inclusive lower bound.</param>
        /// <param name="maxExclusive">The exclusive upper bound; greater than <paramref name="minInclusive"/>.</param>
        /// <returns>The random integer.</returns>
        int NextInt32(int minInclusive, int maxExclusive);
    }
}
