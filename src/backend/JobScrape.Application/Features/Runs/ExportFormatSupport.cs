using JobScrape.Application.Abstractions;

namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Feature-flag mapping for the export formats: which values the host
    /// currently serves and their query-string names. The controller default
    /// and the handler's error copy both read this, so the two cannot drift.
    /// </summary>
    public static class ExportFormatSupport
    {
        /// <summary>
        /// Lists the enabled formats in csv-to-xlsx order. Both off yields an
        /// empty list.
        /// </summary>
        /// <param name="flags">The feature flags bound at host startup. Must not be null.</param>
        /// <returns>The query-string names of the enabled formats.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flags"/> is null.</exception>
        public static IReadOnlyList<string> Enabled(FeatureFlags flags)
        {
            ArgumentNullException.ThrowIfNull(flags);
            var enabled = new List<string>(2);
            if (flags.CsvExport)
            {
                enabled.Add("csv");
            }

            if (flags.ExcelExport)
            {
                enabled.Add("xlsx");
            }

            return enabled;
        }

        /// <summary>
        /// True when the format's flag is on. Undefined values answer false.
        /// </summary>
        /// <param name="format">The format to probe.</param>
        /// <param name="flags">The feature flags bound at host startup. Must not be null.</param>
        /// <returns>Whether the format may be served.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flags"/> is null.</exception>
        public static bool IsEnabled(ExportFormat format, FeatureFlags flags)
        {
            ArgumentNullException.ThrowIfNull(flags);
            return format switch
            {
                ExportFormat.Csv => flags.CsvExport,
                ExportFormat.Xlsx => flags.ExcelExport,
                _ => false,
            };
        }
    }
}
