using System.Text;
using JobScrape.Application.Features.Runs;

namespace JobScrape.Infrastructure.Export
{
    /// <summary>
    /// Renders a run's postings as UTF-8 CSV with a BOM (so spreadsheet
    /// apps detect the encoding) and an RFC 4180 header line. Pure
    /// renderer: bounded by the handler's row cap, no disk writes.
    /// </summary>
    public sealed class CsvPostingWriter : IPostingExportWriter
    {
        /// <summary>
        /// The byte-order mark that tells spreadsheet apps the file is UTF-8.
        /// </summary>
        private static readonly byte[] Utf8Preamble = Encoding.UTF8.GetPreamble();

        /// <inheritdoc />
        public ExportFormat Format => ExportFormat.Csv;

        /// <inheritdoc />
        public Task<ExportFile> WriteAsync(
            string fileName,
            IReadOnlyList<ExportPosting> postings,
            CancellationToken cancellationToken
        )
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            ArgumentNullException.ThrowIfNull(postings);

            var text = ExportColumns.CsvText(postings.Select(ExportColumns.Row));
            var body = Encoding.UTF8.GetBytes(text);
            var content = new byte[Utf8Preamble.Length + body.Length];
            Buffer.BlockCopy(Utf8Preamble, 0, content, 0, Utf8Preamble.Length);
            Buffer.BlockCopy(body, 0, content, Utf8Preamble.Length, body.Length);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ExportFile(fileName, "text/csv", content));
        }
    }
}
