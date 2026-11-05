using System.Text.RegularExpressions;

namespace JobScrape.Infrastructure.Sources.Naukri
{
    /// <summary>
    /// A parsed Naukri result-range text such as <c>1 - 20 of 131</c>.
    /// </summary>
    /// <param name="First">One-based index of the first posting on the page.</param>
    /// <param name="Last">One-based index of the last posting on the page.</param>
    /// <param name="Total">Total postings in the listing.</param>
    public sealed record NaukriResultRange(int First, int Last, int Total)
    {
        /// <summary>
        /// Matches <c>1 - 20 of 131</c> with optional thousands separators.
        /// </summary>
        private static readonly Regex RangePattern = new(
            @"^\s*(\d[\d,]*)\s*-\s*(\d[\d,]*)\s+of\s+(\d[\d,]*)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant
        );

        /// <summary>
        /// Gets the page size implied by the range.
        /// </summary>
        public int PageSize => Last - First + 1;

        /// <summary>
        /// Gets the last page number of the listing for this page size.
        /// Only valid for a full first page range: later pages end short,
        /// so never feed them here. The probe always parses page 1.
        /// </summary>
        public int MaxPage => (Total + PageSize - 1) / PageSize;

        /// <summary>
        /// Parses a result-range text, or null when it is missing or malformed.
        /// The parser never throws: commas are stripped before parsing and any
        /// inconsistent range (last before first, total below last) is rejected.
        /// </summary>
        /// <param name="raw">The raw range text. Null or blank yields null.</param>
        /// <returns>The parsed range, or null when unparseable.</returns>
        public static NaukriResultRange? Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var match = RangePattern.Match(raw);
            if (!match.Success)
            {
                return null;
            }

            if (
                !TryNumber(match.Groups[1].Value, out var first)
                || !TryNumber(match.Groups[2].Value, out var last)
                || !TryNumber(match.Groups[3].Value, out var total)
            )
            {
                return null;
            }

            if (first < 1 || last < first || total < last)
            {
                return null;
            }

            return new NaukriResultRange(first, last, total);
        }

        /// <summary>
        /// Parses one group after stripping thousands separators.
        /// </summary>
        /// <param name="text">The digits, possibly with commas.</param>
        /// <param name="value">The parsed value.</param>
        /// <returns>True when the group is a non-negative integer.</returns>
        private static bool TryNumber(string text, out int value)
        {
            return int.TryParse(
                text.Replace(",", string.Empty, StringComparison.Ordinal),
                out value
            );
        }
    }
}
