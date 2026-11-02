using JobScrape.Application.Abstractions;

namespace JobScrape.Infrastructure.Runtime
{
    /// <summary>
    /// <see cref="IDelay"/> over <see cref="TimeProvider"/>, so tests that
    /// inject a fake clock control waiting too.
    /// </summary>
    public sealed class TimeProviderDelay : IDelay
    {
        // Injected and never mutated.
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeProviderDelay"/> class.
        /// </summary>
        /// <param name="clock">The time source.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="clock"/> is null.</exception>
        public TimeProviderDelay(TimeProvider clock)
        {
            ArgumentNullException.ThrowIfNull(clock);
            _clock = clock;
        }

        /// <summary>
        /// Waits for <paramref name="duration"/>; cancellation ends the wait.
        /// </summary>
        /// <param name="duration">How long to wait; non-negative.</param>
        /// <param name="cancellationToken">Token to observe.</param>
        /// <returns>A task that completes after the wait.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="duration"/> is negative.</exception>
        public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
            return Task.Delay(duration, _clock, cancellationToken);
        }
    }
}
