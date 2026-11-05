using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Runs;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Results;
using JobScrape.Domain.Common;
using JobScrape.Infrastructure.Browser;

namespace JobScrape.Api.Runs
{
    /// <summary>
    /// Executes runs in the background, one at a time, each in its own DI
    /// scope (its own unit of work). A start returns as soon as the run is
    /// recorded or refused; the scrape continues after the request ends.
    /// On host start, runs left in an executing stage by a previous stop are
    /// failed; on host stop, the executing run is cancelled cooperatively and
    /// keeps its partial results.
    /// </summary>
    public sealed partial class RunCoordinator : IRunLauncher, IHostedService, IDisposable
    {
        // All injected and never mutated; _active is guarded by _lock.
        private readonly IServiceScopeFactory _scopes;
        private readonly IBrowserSessionPool _pool;
        private readonly TimeProvider _clock;
        private readonly ILogger<RunCoordinator> _logger;
        private readonly CancellationTokenSource _stopping = new();
        private readonly Lock _lock = new();
        private ActiveRun? _active;
        private int _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="RunCoordinator"/> class.
        /// </summary>
        /// <param name="scopes">Creates one scope per run.</param>
        /// <param name="pool">Releases the run's browser when it ends.</param>
        /// <param name="clock">The time source.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public RunCoordinator(
            IServiceScopeFactory scopes,
            IBrowserSessionPool pool,
            TimeProvider clock,
            ILogger<RunCoordinator> logger
        )
        {
            ArgumentNullException.ThrowIfNull(scopes);
            ArgumentNullException.ThrowIfNull(pool);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(logger);
            _scopes = scopes;
            _pool = pool;
            _clock = clock;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<Result<Guid>> StartAsync(
            RunScrapeRequest request,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(request);

            ActiveRun run;
            lock (_lock)
            {
                if (_active is not null)
                {
                    return Result.Fail<Guid>(Errors.RunInProgress(_active.RunId));
                }

                run = new ActiveRun(
                    request.BrowserProfileId,
                    CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token)
                );
                _active = run;
            }

            var execution = Task.Run(() => ExecuteAsync(run, request), CancellationToken.None);
            await Task.WhenAny(run.Started.Task, execution).WaitAsync(cancellationToken);
            if (run.Started.Task.IsCompletedSuccessfully)
            {
                return Result.Ok(await run.Started.Task);
            }

            // Ended without starting: a gate refused it, shutdown cancelled
            // it, or it threw (a 500 through the global handler).
            var refusal = await execution;
            return Result.Fail<Guid>(refusal ?? Errors.RunNotStarted());
        }

        /// <inheritdoc />
        public bool TryCancel(Guid runId)
        {
            CancellationTokenSource? cancellation;
            lock (_lock)
            {
                cancellation = _active?.RunId == runId ? _active.Cancellation : null;
            }

            if (cancellation is null)
            {
                return false;
            }

            try
            {
                // Outside the lock: cancellation callbacks may run the
                // handler's cancel path inline.
                cancellation.Cancel();
                return true;
            }
            catch (ObjectDisposedException)
            {
                // The run ended between the lookup and the signal.
                return false;
            }
        }

        /// <summary>
        /// Fails runs that a previous stop interrupted mid-execution, so no
        /// run stays "searching" forever.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when stale runs are recorded.</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopes.CreateAsyncScope();
            var runs = scope.ServiceProvider.GetRequiredService<IScrapeRunRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var stale = await runs.ListExecutingAsync(cancellationToken);
            if (stale.Count == 0)
            {
                return;
            }

            var now = _clock.GetUtcNow();
            foreach (var run in stale)
            {
                run.Fail("Interrupted: the API stopped while the run was executing.", now);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            LogInterrupted(_logger, stale.Count);
        }

        /// <summary>
        /// Cancels the executing run and waits for it to record its partial
        /// results, within the host's shutdown timeout.
        /// </summary>
        /// <param name="cancellationToken">Signalled when the shutdown timeout expires.</param>
        /// <returns>A task that completes when the run has stopped or the timeout expired.</returns>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _stopping.CancelAsync();
            }
            catch (ObjectDisposedException)
            {
                // The container may dispose this singleton before the host
                // stops its hosted services; Dispose already signalled.
                return;
            }

            Task? execution;
            lock (_lock)
            {
                execution = _active?.Finished.Task;
            }

            if (execution is not null)
            {
                await execution.WaitAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Signals shutdown to any executing run, then releases the signal.
        /// Idempotent: the container disposes this instance once per
        /// registration that resolves it (itself, the launcher, the hosted
        /// service).
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _stopping.Cancel();
            _stopping.Dispose();
        }

        /// <summary>
        /// Runs the scrape in its own scope and cleans up after it.
        /// </summary>
        /// <param name="run">The active-run record.</param>
        /// <param name="request">The run request.</param>
        /// <returns>The gate's error when the run was refused; otherwise null.</returns>
        private async Task<ErrorInfo?> ExecuteAsync(ActiveRun run, RunScrapeRequest request)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<RunScrapeHandler>();
                var result = await handler.HandleAsync(
                    request,
                    id => run.MarkStarted(id),
                    run.Cancellation.Token
                );
                return result.IsFailure ? result.Error : null;
            }
            catch (OperationCanceledException) when (run.Cancellation.IsCancellationRequested)
            {
                // Recorded as Cancelled by the handler; nothing to report.
                return null;
            }
            catch (Exception ex) when (run.Started.Task.IsCompleted)
            {
                // Recorded as Failed by the handler; the request has already
                // returned, so the log is the only place left to report it.
                LogRunCrashed(_logger, ex, run.RunId);
                return null;
            }
            finally
            {
                await ReleaseAsync(run);
            }
        }

        /// <summary>
        /// Closes the run's browser and frees the single run slot.
        /// </summary>
        /// <param name="run">The finished run.</param>
        /// <returns>A task that completes when the slot is free.</returns>
        private async Task ReleaseAsync(ActiveRun run)
        {
            try
            {
                await _pool.ReleaseAsync(run.BrowserProfileId);
            }
            catch (Exception ex)
            {
                // A browser that fails to close must not keep the slot taken.
                LogReleaseFailed(_logger, ex, run.RunId);
            }

            lock (_lock)
            {
                if (ReferenceEquals(_active, run))
                {
                    _active = null;
                }
            }

            run.Cancellation.Dispose();
            run.Finished.TrySetResult();
        }

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Failed {Count} runs interrupted by the previous stop"
        )]
        private static partial void LogInterrupted(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Error, Message = "Run {RunId} crashed")]
        private static partial void LogRunCrashed(ILogger logger, Exception exception, Guid? runId);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Browser for run {RunId} did not close cleanly"
        )]
        private static partial void LogReleaseFailed(
            ILogger logger,
            Exception exception,
            Guid? runId
        );

        /// <summary>
        /// The executing run: its cancellation, its id once recorded, and
        /// completion signals.
        /// </summary>
        /// <param name="browserProfileId">The browser profile the run uses.</param>
        /// <param name="cancellation">Cancels the run; linked to host shutdown.</param>
        private sealed class ActiveRun(
            BrowserProfileId browserProfileId,
            CancellationTokenSource cancellation
        )
        {
            /// <summary>
            /// Gets the browser profile the run uses.
            /// </summary>
            public BrowserProfileId BrowserProfileId { get; } = browserProfileId;

            /// <summary>
            /// Gets the run's cancellation source.
            /// </summary>
            public CancellationTokenSource Cancellation { get; } = cancellation;

            /// <summary>
            /// Gets the run id once recorded; null while gates run.
            /// </summary>
            public Guid? RunId { get; private set; }

            /// <summary>
            /// Gets the signal completed with the run id once recorded.
            /// </summary>
            public TaskCompletionSource<Guid> Started { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Gets the signal completed when the run has fully ended.
            /// </summary>
            public TaskCompletionSource Finished { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Records the run id and releases the waiting start request.
            /// </summary>
            /// <param name="id">The recorded run's id.</param>
            public void MarkStarted(ScrapeRunId id)
            {
                RunId = id.Value;
                Started.TrySetResult(id.Value);
            }
        }
    }
}
