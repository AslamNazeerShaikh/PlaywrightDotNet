namespace JobScrape.Application.Features.Health
{
    /// <summary>
    /// Explicit use-case handler for liveness. Called directly by the API
    /// controller; no mediator, no bus. Time comes from
    /// <see cref="TimeProvider"/> so tests are deterministic.
    /// </summary>
    public sealed class GetHealthHandler
    {
        // Injected, never mutated: the handler stays stateless and safe to
        // register as a transient that concurrent requests share nothing through.
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetHealthHandler"/> class.
        /// </summary>
        /// <param name="clock">The clock used to timestamp the response. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="clock"/> is null.</exception>
        public GetHealthHandler(TimeProvider clock)
        {
            ArgumentNullException.ThrowIfNull(clock);
            _clock = clock;
        }

        /// <summary>
        /// Returns the current liveness payload. Liveness cannot fail
        /// operationally, so the result is a DTO rather than a
        /// <c>Result</c>; infrastructure failures surface as exceptions.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task whose result is the current health payload.</returns>
        public Task<GetHealthResponse> HandleAsync(CancellationToken cancellationToken)
        {
            // Synchronous work wrapped for the async handler contract.
            // Task.FromResult avoids an async state machine for a method
            // that never awaits; cancellationToken is accepted to keep the
            // uniform handler signature for future I/O (e.g. disk checks).
            var response = new GetHealthResponse("healthy", _clock.GetUtcNow());
            return Task.FromResult(response);
        }
    }
}
