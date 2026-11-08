using System.Globalization;
using System.Text;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Exports one run's postings to CSV or XLSX. No format string reaches
    /// the writers: the edge parses the query into <see cref="ExportFormat"/>,
    /// so an unsupported format is rejected here before any store read, and
    /// a defined format whose feature flag is off answers
    /// <c>FEATURE_DISABLED</c> at the same point.
    /// The file is named for the run's source, profile, creation stamp, and
    /// short run id, so downloads sort and identify without opening.
    /// </summary>
    public sealed class ExportRunPostingsHandler
    {
        /// <summary>
        /// How many postings an export carries at most. Bounded so a large
        /// run cannot exhaust memory; the cockpit pages the same data.
        /// </summary>
        public const int Limit = RunLimits.MaxJobsCeiling;

        /// <summary>
        /// Longest profile slug kept in a file name. Download names stay
        /// well under filesystem limits however long the profile name is.
        /// </summary>
        public const int MaxProfileSlugLength = 40;

        // All injected and never mutated.
        private readonly IRunStore _store;
        private readonly IReadOnlyList<IPostingExportWriter> _writers;
        private readonly FeatureFlags _flags;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportRunPostingsHandler"/> class.
        /// </summary>
        /// <param name="store">The run read store.</param>
        /// <param name="writers">The format writers; at most one per format. Must not be null.</param>
        /// <param name="flags">The feature flags bound at host startup. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public ExportRunPostingsHandler(
            IRunStore store,
            IEnumerable<IPostingExportWriter> writers,
            FeatureFlags flags
        )
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(writers);
            ArgumentNullException.ThrowIfNull(flags);
            _store = store;
            _writers = writers.ToArray();
            _flags = flags;
        }

        /// <summary>
        /// Exports up to <see cref="Limit"/> postings for the run.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="format">The file format.</param>
        /// <param name="group">Which company slice to export.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// The rendered file; <c>VALIDATION_FAILED</c> for an empty id or a
        /// company slice without a company list, <c>RUN_NOT_FOUND</c> for a
        /// missing run, <c>FEATURE_DISABLED</c> when
        /// <paramref name="format"/> is turned off, or
        /// <c>EXPORT_FORMAT_UNSUPPORTED</c> when <paramref name="format"/>
        /// is undefined or no writer renders it.
        /// </returns>
        public async Task<Result<ExportFile>> HandleAsync(
            Guid runId,
            ExportFormat format,
            CompanyExportGroup group,
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
                    Errors.ExportFormatUnsupported(
                        format.ToString(),
                        ExportFormatSupport.Enabled(_flags)
                    )
                );
            }

            if (!Enum.IsDefined(group))
            {
                return Result.Fail<ExportFile>(
                    Errors.ValidationFailed(
                        "The request contains invalid fields.",
                        new Dictionary<string, string[]>
                        {
                            ["group"] = ["Choose all, only, or exclude."],
                        }
                    )
                );
            }

            if (!ExportFormatSupport.IsEnabled(format, _flags))
            {
                // Disabled answers the feature-disabled 404 before any writer
                // lookup or store read: the caller cannot distinguish
                // "absent" from "off", and no disabled work is constructed.
                return Result.Fail<ExportFile>(Errors.FeatureDisabled());
            }

            var writer = _writers.FirstOrDefault(candidate => candidate.Format == format);
            if (writer is null)
            {
                return Result.Fail<ExportFile>(
                    Errors.ExportFormatUnsupported(
                        format.ToString(),
                        ExportFormatSupport.Enabled(_flags)
                    )
                );
            }

            var run = await _store.GetAsync(runId, cancellationToken);
            if (run is null)
            {
                return Result.Fail<ExportFile>(Errors.RunNotFound(runId));
            }

            if (group != CompanyExportGroup.All && run.Companies.Count == 0)
            {
                return Result.Fail<ExportFile>(Errors.ExportCompaniesMissing());
            }

            var postings = await _store.ListExportPostingsAsync(runId, Limit, cancellationToken);
            if (postings is null)
            {
                return Result.Fail<ExportFile>(Errors.RunNotFound(runId));
            }

            var companySet = new HashSet<string>(run.Companies, StringComparer.OrdinalIgnoreCase);
            var filtered = group switch
            {
                CompanyExportGroup.Only => postings
                    .Where(posting => Matches(posting.Company, companySet))
                    .ToArray(),
                CompanyExportGroup.Exclude => postings
                    .Where(posting => !Matches(posting.Company, companySet))
                    .ToArray(),
                _ => postings,
            };

            return Result.Ok(
                await writer.WriteAsync(FileName(format, run, group), filtered, cancellationToken)
            );
        }

        /// <summary>
        /// Names the download: source, profile slug, UTC creation stamp, and
        /// an 8-character run prefix. The run stamp (not the export time)
        /// keeps repeated exports of one run byte-identical in name.
        /// </summary>
        /// <param name="format">The file format; selects the extension.</param>
        /// <param name="run">The run being exported. Must not be null.</param>
        /// <param name="group">The company slice, reflected in the suffix.</param>
        /// <returns>The download name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="run"/> is null.</exception>
        private static string FileName(
            ExportFormat format,
            RunResponse run,
            CompanyExportGroup group
        )
        {
            ArgumentNullException.ThrowIfNull(run);
            var extension = format == ExportFormat.Csv ? "csv" : "xlsx";
            var stamp = run
                .CreatedAt.ToUniversalTime()
                .ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            var suffix = group switch
            {
                CompanyExportGroup.Only => "-only-companies",
                CompanyExportGroup.Exclude => "-exclude-companies",
                _ => string.Empty,
            };
            return string.Join(
                    '-',
                    run.SourceKey,
                    Slug(run.ProfileName),
                    stamp,
                    run.Id.ToString("N")[..8]
                ) + $"-postings{suffix}.{extension}";
        }

        private static bool Matches(string? company, HashSet<string> companies)
        {
            return !string.IsNullOrWhiteSpace(company) && companies.Contains(company.Trim());
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
