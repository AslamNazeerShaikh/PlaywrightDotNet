namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Renders a run's postings in one <see cref="ExportFormat"/>.
    /// Implementations live in Infrastructure (CSV here, ClosedXML for
    /// Excel); the handler picks one by format. Writers are pure renderers:
    /// bounded row counts, no disk writes, cancellation observed.
    /// </summary>
    public interface IPostingExportWriter
    {
        /// <summary>
        /// Gets the format this writer renders.
        /// </summary>
        ExportFormat Format { get; }

        /// <summary>
        /// Renders the postings to a file.
        /// </summary>
        /// <param name="runId">The run the postings came from; names the file.</param>
        /// <param name="postings">The postings, most recently seen first. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The rendered file.</returns>
        Task<ExportFile> WriteAsync(
            Guid runId,
            IReadOnlyList<ExportPosting> postings,
            CancellationToken cancellationToken
        );
    }
}
