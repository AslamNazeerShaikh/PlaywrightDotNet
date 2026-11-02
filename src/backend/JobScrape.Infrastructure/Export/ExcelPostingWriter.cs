using ClosedXML.Excel;
using JobScrape.Application.Features.Runs;

namespace JobScrape.Infrastructure.Export
{
    /// <summary>
    /// Renders a run's postings as an Excel workbook (docs/16-library-usage-policy.md):
    /// one <c>Jobs</c> sheet, the header frozen, the posting URL a clickable
    /// hyperlink, and the workbook disposed before the bytes are read. Pure
    /// renderer: bounded by the handler's row cap, no disk writes.
    /// </summary>
    public sealed class ExcelPostingWriter : IPostingExportWriter
    {
        /// <inheritdoc />
        public ExportFormat Format => ExportFormat.Xlsx;

        /// <inheritdoc />
        public Task<ExportFile> WriteAsync(
            string fileName,
            IReadOnlyList<ExportPosting> postings,
            CancellationToken cancellationToken
        )
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            ArgumentNullException.ThrowIfNull(postings);

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Jobs");
            for (var column = 0; column < ExportColumns.Headers.Length; column++)
            {
                sheet.Cell(1, column + 1).Value = ExportColumns.Headers[column];
            }

            sheet.SheetView.FreezeRows(1);
            var row = 2;
            foreach (var posting in postings)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var values = ExportColumns.Row(posting);
                for (var column = 0; column < values.Length; column++)
                {
                    sheet.Cell(row, column + 1).Value = values[column];
                }

                // Column 9 is the posting URL: link it so it opens in Excel.
                sheet.Cell(row, 9).SetHyperlink(new XLHyperlink(posting.Url));
                row++;
            }

            sheet.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return Task.FromResult(
                new ExportFile(
                    fileName,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    stream.ToArray()
                )
            );
        }
    }
}
