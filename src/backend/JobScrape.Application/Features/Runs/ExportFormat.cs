namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// The file formats a run's postings can be exported in. The format is
    /// a closed enum so an unsupported value is unrepresentable at the
    /// handler boundary; the API edge parses the query string into it.
    /// </summary>
    public enum ExportFormat
    {
        /// <summary>
        /// Comma-separated values, UTF-8 with a BOM for spreadsheet apps.
        /// </summary>
        Csv = 0,

        /// <summary>
        /// Excel workbook with the posting URL as a hyperlink.
        /// </summary>
        Xlsx = 1,
    }
}
