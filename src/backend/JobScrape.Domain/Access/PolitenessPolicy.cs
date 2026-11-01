using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Computes the jittered delay between two requests of a run
    /// (docs/03-domain-model.md §8.1). The delay never leaves the run's
    /// [<see cref="RunLimits.MinDelay"/>, <see cref="RunLimits.MaxDelay"/>]
    /// window, even if the random source misbehaves. Sleeping is the
    /// infrastructure's job; this type only decides how long.
    /// </summary>
    public sealed class PolitenessPolicy
    {
        // Both injected and never mutated.
        private readonly RunLimits _limits;
        private readonly IRandomSource _random;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="limits">The run's delay window. Must not be null.</param>
        /// <param name="random">The jitter source. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public PolitenessPolicy(RunLimits limits, IRandomSource random)
        {
            ArgumentNullException.ThrowIfNull(limits);
            ArgumentNullException.ThrowIfNull(random);
            _limits = limits;
            _random = random;
        }

        /// <summary>
        /// Returns the next delay, uniformly jittered to the millisecond.
        /// </summary>
        /// <returns>A delay within the run's window, inclusive.</returns>
        public TimeSpan NextDelay()
        {
            // RunLimits caps MaxDelay at 2 minutes, so milliseconds fit an int.
            var min = (int)_limits.MinDelay.TotalMilliseconds;
            var max = (int)_limits.MaxDelay.TotalMilliseconds;
            var jittered = Math.Clamp(_random.NextInt32(min, max + 1), min, max);
            return TimeSpan.FromMilliseconds(jittered);
        }
    }
}
