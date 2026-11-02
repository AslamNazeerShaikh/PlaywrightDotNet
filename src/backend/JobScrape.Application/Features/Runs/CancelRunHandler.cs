using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain.Common;

namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Cancels a run, idempotently. An executing run is signalled and
    /// cancels cooperatively (the current page may finish, partial results
    /// are kept); a paused run that is not executing is cancelled directly;
    /// a terminal run is returned as it is.
    /// </summary>
    public sealed class CancelRunHandler
    {
        // All injected and never mutated.
        private readonly IRunLauncher _launcher;
        private readonly IScrapeRunRepository _runs;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRunStore _store;
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="CancelRunHandler"/> class.
        /// </summary>
        /// <param name="launcher">Signals executing runs.</param>
        /// <param name="runs">Loads a run that is not executing.</param>
        /// <param name="unitOfWork">Commits a direct cancellation.</param>
        /// <param name="store">Reads the run back.</param>
        /// <param name="clock">The time source.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public CancelRunHandler(
            IRunLauncher launcher,
            IScrapeRunRepository runs,
            IUnitOfWork unitOfWork,
            IRunStore store,
            TimeProvider clock
        )
        {
            ArgumentNullException.ThrowIfNull(launcher);
            ArgumentNullException.ThrowIfNull(runs);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(clock);
            _launcher = launcher;
            _runs = runs;
            _unitOfWork = unitOfWork;
            _store = store;
            _clock = clock;
        }

        /// <summary>
        /// Cancels the run, or reports it unchanged when already terminal.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The run as it stands; <c>VALIDATION_FAILED</c> for an empty id; or <c>RUN_NOT_FOUND</c>.</returns>
        public async Task<Result<RunResponse>> HandleAsync(
            Guid id,
            CancellationToken cancellationToken
        )
        {
            if (id == Guid.Empty)
            {
                return Result.Fail<RunResponse>(RunIds.Required());
            }

            if (!_launcher.TryCancel(id))
            {
                var run = await _runs.GetAsync(ScrapeRunId.From(id), cancellationToken);
                if (run is null)
                {
                    return Result.Fail<RunResponse>(Errors.RunNotFound(id));
                }

                if (!run.IsTerminal)
                {
                    run.Cancel(_clock.GetUtcNow());
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }

            var current = await _store.GetAsync(id, cancellationToken);
            return current is null
                ? Result.Fail<RunResponse>(Errors.RunNotFound(id))
                : Result.Ok(current);
        }
    }
}
