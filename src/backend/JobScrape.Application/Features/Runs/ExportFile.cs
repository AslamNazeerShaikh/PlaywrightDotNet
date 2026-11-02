namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// A rendered export file, ready for the API edge to return.
    /// </summary>
    /// <param name="FileName">The download name, e.g. <c>naukri-backend-dev-20261102T080000Z-a1b2c3d4-postings.csv</c>.</param>
    /// <param name="ContentType">The MIME type.</param>
    /// <param name="Content">The file bytes.</param>
    public sealed record ExportFile(string FileName, string ContentType, byte[] Content);
}
