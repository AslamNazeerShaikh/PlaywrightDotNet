using System.Globalization;
using System.Text;
using JobScrape.Application.Results;

namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Exports one run's postings to CSV or XLSX. No format string reaches
    /// the writers: the edge parses the query into <see cref="ExportFormat"/>,
    /// so an unsupported format is rejected here before any store read.
    /// The file is named for the run's source, profile, creation stamp, and
    /// short run id, so downloads sort and identify without opening.
    /// </summary>
    public sealed class ExportRunPostingsHandler
    {
        /// <summary>
        /// How many postings an export carries at most. Bounded so a large
        /// run cannot exhaust memory; the cockpit pages the same data.
        /// </summary>
        public const int Limit = 5000;

        /// <summary>
        /// Longest profile slug kept in a file name. Download names stay
        /// well under filesystem limits however long the profile name is.
        /// </summary>
        public const int MaxProfileSlugLength = 40;

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
                return Result.Fail<ExportFile>(Errors.ExportFormatUnsupported(format.ToString()));
            }

            var writer = _writers.FirstOrDefault(candidate => candidate.Format == format);
            if (writer is null)
            {
                return Result.Fail<ExportFile>(Errors.ExportFormatUnsupported(format.ToString()));
            }

            var run = await _store.GetAsync(runId, cancellationToken);
            if (run is null)
            {
                return Result.Fail<ExportFile>(Errors.RunNotFound(runId));
            }

            var postings = await _store.ListExportPostingsAsync(runId, Limit, cancellationToken);
            if (postings is null)
            {
                return Result.Fail<ExportFile>(Errors.RunNotFound(runId));
            }

            return Result.Ok(
                await writer.WriteAsync(FileName(format, run), postings, cancellationToken)
            );
        }

        /// <summary>
        /// Names the download: source, profile slug, UTC creation stamp, and
        /// an 8-character run prefix. The run stamp (not the export time)
        /// keeps repeated exports of one run byte-identical in name.
        /// </summary>
        /// <param name="format">The file format; selects the extension.</param>
        /// <param name="run">The run being exported. Must not be null.</param>
        /// <returns>The download name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="run"/> is null.</exception>
        private static string FileName(ExportFormat format, RunResponse run)
        {
            ArgumentNullException.ThrowIfNull(run);
            var extension = format == ExportFormat.Csv ? "csv" : "xlsx";
            var stamp = run
                .CreatedAt.ToUniversalTime()
                .ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            return string.Join(
                    '-',
                    run.SourceKey,
                    Slug(run.ProfileName),
                    stamp,
                    run.Id.ToString("N")[..8]
                ) + $"-postings.{extension}";
        }

        /// <summary>
        /// Turns a profile name into a file-safe slug: lower-case ASCII
        /// letters and digits, other runs collapsed to one dash. Falls back
        /// to <c>profile</c> when the run outlived its profile or the name
        /// has no slug characters.
        /// </summary>
        /// <param name="name">The profile name, if the profile still exists.</param>
        /// <returns>The slug, at most <see cref="MaxProfileSlugLength"/> characters.</returns>
        private static string Slug(string? name)
        {
            var builder = new StringBuilder();
            var dash = false;
            foreach (var character in name ?? string.Empty)
            {
                if (char.IsAsciiLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                    dash = false;
                }
                else if (!dash && builder.Length > 0)
                {
                    builder.Append('-');
                    dash = true;
                }
            }

            var slug = builder.ToString().TrimEnd('-');
            if (slug.Length > MaxProfileSlugLength)
            {
                slug = slug[..MaxProfileSlugLength].TrimEnd('-');
            }

            return slug.Length == 0 ? "profile" : slug;
        }
    }
}
