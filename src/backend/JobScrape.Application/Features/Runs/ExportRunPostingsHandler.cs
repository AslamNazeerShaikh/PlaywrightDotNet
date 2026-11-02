using JobScrape.Application.Results;

namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Exports one run's postings to CSV or XLSX. No format string reaches
    /// the writers: the edge parses the query into <see cref="ExportFormat"/>,
    /// so an unsupported format is rejected here before any store read.
    /// </summary>
    public sealed class ExportRunPostingsHandler
    {
        /// <summary>
        /// How many postings an export carries at most. Bounded so a large
        /// run cannot exhaust memory; the cockpit pages the same data.
        /// </summary>
        public const int Limit = 5000;

        // All injected and never mutated.
        private readonly IRunStore _store;
        private readonly IReadOnlyList<IPostingExportWriter> _writers;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportRunPostingsHandler"/> class.
        /// </summary>
        /// <param name="store">The run read store.</param>
        /// <param name="writers">The format writers; at most one per format. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public ExportRunPostingsHandler(IRunStore store, IEnumerable<IPostingExportWriter> writers)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(writers);
            _store = store;
            _writers = writers.ToArray();
        }

        /// <summary>
        /// Exports up to <see cref="Limit"/> postings for the run.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="format">The file format.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// The rendered file; <c>VALIDATION_FAILED</c> for an empty id,
        /// <c>RUN_NOT_FOUND</c> for a missing run, or
        /// <c>EXPORT_FORMAT_UNSUPPORTED</c> when <paramref name="format"/>
        /// is undefined or no writer renders it.
        /// </returns>
        public async Task<Result<ExportFile>> HandleAsync(
            Guid runId,
            ExportFormat format,
            CancellationToken cancellationToken
        )
        {
            if (runId == Guid.Empty)
            {
                return Result.Fail<ExportFile>(RunIds.Required());
            }

            if (!Enum.IsDefined(format))
            {
                return Result.Fail<ExportFile>(
                    Errors.ExportFormatUnsupported(format.ToString())
                );
            }

            var writer = _writers.FirstOrDefault(candidate => candidate.Format == format);
            if (writer is null)
            {
                return Result.Fail<ExportFile>(
                    Errors.ExportFormatUnsupported(format.ToString())
                );
            }

            var postings = await _store.ListExportPostingsAsync(runId, Limit, cancellationToken);
            if (postings is null)
            {
                return Result.Fail<ExportFile>(Errors.RunNotFound(runId));
            }

            return Result.Ok(
                await writer.WriteAsync(runId, postings, cancellationToken)
            );
        }
    }
}
