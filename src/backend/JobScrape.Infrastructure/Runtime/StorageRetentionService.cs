using JobScrape.Infrastructure.Browser;
using JobScrape.Infrastructure.Configuration;
using JobScrape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobScrape.Infrastructure.Runtime
{
    /// <summary>
    /// Enforces the storage retention windows: run artifacts older than
    /// <see cref="StorageOptions.RetentionDays"/> leave the artifacts root,
    /// and postings not seen within
    /// <see cref="StorageOptions.PostingRetentionDays"/> leave the database.
    /// The sweep runs once at host start and then daily; failures are logged
    /// and retried on the next pass.
    /// </summary>
    public sealed partial class StorageRetentionService : BackgroundService
    {
        /// <summary>
        /// How often the sweep runs after the initial pass.
        /// </summary>
        internal static readonly TimeSpan SweepInterval = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopes;
        private readonly BrowserSessionSettings _settings;
        private readonly StorageOptions _storage;
        private readonly IFileSystem _files;
        private readonly TimeProvider _clock;
        private readonly ILogger<StorageRetentionService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageRetentionService"/> class.
        /// </summary>
        /// <param name="scopes">Creates the database scope per sweep.</param>
        /// <param name="settings">Provides the resolved artifacts root.</param>
        /// <param name="storage">Provides both retention windows.</param>
        /// <param name="files">Enumerates and deletes artifact directories.</param>
        /// <param name="clock">The time source.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public StorageRetentionService(
            IServiceScopeFactory scopes,
            BrowserSessionSettings settings,
            IOptions<StorageOptions> storage,
            IFileSystem files,
            TimeProvider clock,
            ILogger<StorageRetentionService> logger
        )
        {
            ArgumentNullException.ThrowIfNull(scopes);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(storage);
            ArgumentNullException.ThrowIfNull(files);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(logger);
            _scopes = scopes;
            _settings = settings;
            _storage = storage.Value;
            _files = files;
            _clock = clock;
            _logger = logger;
        }

        /// <summary>
        /// Sweeps once, then daily until shutdown.
        /// </summary>
        /// <param name="stoppingToken">Signalled when the host stops.</param>
        /// <returns>A task that completes when the loop exits.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SweepAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    LogSweepFailed(_logger, exception);
                }

                try
                {
                    await Task.Delay(SweepInterval, _clock, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Runs one retention pass: postings first, then artifact directories.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the pass is done.</returns>
        internal async Task SweepAsync(CancellationToken cancellationToken)
        {
            var now = _clock.GetUtcNow();
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<JobScrapeDbContext>();
            var postingCutoff = now.AddDays(-_storage.PostingRetentionDays);
            var postings = await db
                .JobPostings.Where(posting => posting.LastSeenAt < postingCutoff)
                .ExecuteDeleteAsync(cancellationToken);

            var artifactCutoff = now.AddDays(-_storage.RetentionDays);
            var runsRoot = _files.Combine(_settings.ArtifactsRoot, "runs");
            var directories = 0;
            foreach (var directory in _files.EnumerateDirectories(runsRoot))
            {
                try
                {
                    if (_files.GetLastWriteTimeUtc(directory) >= artifactCutoff)
                    {
                        continue;
                    }

                    _files.DeleteDirectory(directory, recursive: true);
                    directories++;
                }
                catch (Exception exception)
                    when (exception is IOException or UnauthorizedAccessException)
                {
                    LogDeleteFailed(_logger, exception, directory);
                }
            }

            LogSwept(_logger, postings, directories);
        }

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "A storage retention sweep failed; the next pass retries"
        )]
        private static partial void LogSweepFailed(ILogger logger, Exception exception);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Storage retention removed {Postings} postings and {Directories} artifact directories"
        )]
        private static partial void LogSwept(ILogger logger, int postings, int directories);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Deleting expired artifact directory {Directory} failed"
        )]
        private static partial void LogDeleteFailed(
            ILogger logger,
            Exception exception,
            string directory
        );
    }
}
