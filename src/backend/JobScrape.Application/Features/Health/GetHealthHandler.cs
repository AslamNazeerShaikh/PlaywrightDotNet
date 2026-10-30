namespace JobScrape.Application.Features.Health
{
    /// <summary>
    /// Explicit use-case handler for liveness. Called directly by the API
    /// controller; no mediator, no bus. Time comes from
    /// <see cref="TimeProvider"/> so tests are deterministic.
    /// </summary>
    public sealed class GetHealthHandler
    {
        private readonly TimeProvider _clock;

        public GetHealthHandler(TimeProvider clock)
        {
            ArgumentNullException.ThrowIfNull(clock);
            _clock = clock;
        }

        public Task<GetHealthResponse> HandleAsync(CancellationToken cancellationToken)
        {
            var response = new GetHealthResponse("healthy", _clock.GetUtcNow());
            return Task.FromResult(response);
        }
    }
}
