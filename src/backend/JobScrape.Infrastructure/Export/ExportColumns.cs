using System.Globalization;
using System.Text;

namespace JobScrape.Infrastructure.Export
{
    /// <summary>
    /// Column order and headers shared by the posting writers: both files
    /// carry the same columns in the same order.
    /// </summary>
    internal static class ExportColumns
    {
        /// <summary>
        /// The headers, in column order.
        /// </summary>
        public static readonly string[] Headers =
        [
            "Title",
            "Company",
            "Location",
            "Work mode",
            "Experience",
            "Salary",
            "Posted",
            "Skills",
            "Posting URL",
            "Apply URL",
            "External ID",
            "Source",
            "Description",
            "First seen",
            "Last seen",
        ];

        /// <summary>
        /// Renders one posting as the column values in <see cref="Headers"/> order.
        /// </summary>
        /// <param name="posting">The posting.</param>
        /// <returns>The cell texts.</returns>
        public static string[] Row(Application.Features.Runs.ExportPosting posting)
        {
            return
            [
                posting.Title,
                posting.Company ?? string.Empty,
                posting.Location ?? string.Empty,
                posting.WorkMode,
                posting.Experience ?? string.Empty,
                posting.Salary ?? string.Empty,
                posting.PostedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    ?? string.Empty,
                string.Join("; ", posting.Skills),
                posting.Url,
                posting.ApplyUrl ?? string.Empty,
                posting.ExternalJobId,
                posting.Source,
                posting.Description ?? string.Empty,
                posting.FirstSeenAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                posting.LastSeenAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ];
        }

        /// <summary>
        /// Quotes one CSV field: wrapped in quotes when it holds a comma,
        /// quote, or line break, with inner quotes doubled (RFC 4180). A
        /// leading <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tab, or carriage
        /// return is prefixed with an apostrophe so spreadsheet apps open
        /// scraped text as text instead of executing it as a formula.
        /// </summary>
        /// <param name="value">The field text.</param>
        /// <returns>The escaped field.</returns>
        public static string CsvField(string value)
        {
            var safe = SafeCell(value);
            return
                safe.Contains(',')
                || safe.Contains('"')
                || safe.Contains('\n')
                || safe.Contains('\r')
                ? $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
                : safe;
        }

        /// <summary>
        /// Neutralizes a cell that a spreadsheet app would otherwise run as a
        /// formula. Scraped text is untrusted: any cell whose first character
        /// is <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tab, or carriage return
        /// gets a leading apostrophe.
        /// </summary>
        /// <param name="value">The cell text.</param>
        /// <returns>The cell text, safe to open in a spreadsheet app.</returns>
        public static string SafeCell(string value)
        {
            return value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
                ? string.Concat("'", value)
                : value;
        }

        /// <summary>
        /// Renders rows as CSV text with a header line.
        /// </summary>
        /// <param name="rows">The data rows.</param>
        /// <returns>The CSV text.</returns>
        public static string CsvText(IEnumerable<string[]> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(',', Headers.Select(CsvField)));
            foreach (var row in rows)
            {
                builder.AppendLine(string.Join(',', row.Select(CsvField)));
            }

            return builder.ToString();
        }
    }
}
