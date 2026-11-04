using ClosedXML.Excel;
using JobScrape.Application.Features.Runs;
using JobScrape.Infrastructure.Export;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Export
{
    public sealed class PostingWriterTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static ExportPosting Posting(
            string externalId = "101",
            string title = "Senior .NET Developer, Backend",
            string? description = "Build services.\nSecond line with \"quotes\"."
        )
        {
            return new ExportPosting(
                Guid.NewGuid(),
                externalId,
                "naukri",
                $"https://www.naukri.com/job-listings-dev-{externalId}",
                "https://careers.contoso.example/apply/101",
                title,
                "Contoso",
                "Pune",
                "Unknown",
                "5-8 Yrs",
                "15-27.5 Lacs PA",
                new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero),
                ["C#", ".NET"],
                description,
                Now.AddDays(-2),
                Now
            );
        }

        [Fact]
        public async Task CsvHasHeaderAndQuoting()
        {
            var writer = new CsvPostingWriter();

            var file = await writer.WriteAsync("naukri-backend-dev-postings.csv", [Posting()], Ct);
            var text = System.Text.Encoding.UTF8.GetString(file.Content);

            Assert.Equal("naukri-backend-dev-postings.csv", file.FileName);
            Assert.Equal("text/csv", file.ContentType);
            // The BOM survives: spreadsheet apps detect UTF-8.
            Assert.Equal(0xEF, file.Content[0]);
            Assert.StartsWith("Title,Company,Location,", text);
            // Commas, quotes, and newlines are all quoted and escaped.
            Assert.Contains("\"Senior .NET Developer, Backend\"", text);
            Assert.Contains("\"Build services.\nSecond line with \"\"quotes\"\".\"", text);
        }

        [Fact]
        public async Task CsvIsEmptyButHeaderedWithoutPostings()
        {
            var writer = new CsvPostingWriter();

            var file = await writer.WriteAsync("naukri-backend-dev-postings.csv", [], Ct);

            Assert.Equal("naukri-backend-dev-postings.csv", file.FileName);
            Assert.StartsWith(
                "Title,Company,Location,",
                System.Text.Encoding.UTF8.GetString(file.Content)
            );
        }

        [Fact]
        public async Task XlsxHyperlinkOpensThePosting()
        {
            var writer = new ExcelPostingWriter();

            var file = await writer.WriteAsync("naukri-backend-dev-postings.xlsx", [Posting()], Ct);

            Assert.Equal("naukri-backend-dev-postings.xlsx", file.FileName);
            Assert.Equal(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                file.ContentType
            );
            using var stream = new MemoryStream(file.Content);
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheet("Jobs");
            Assert.Equal("Title", sheet.Cell(1, 1).GetString());
            Assert.Equal("Senior .NET Developer, Backend", sheet.Cell(2, 1).GetString());
            var link = sheet.Cell(2, 9).GetHyperlink();
            Assert.True(link.IsExternal);
            Assert.Equal(
                "https://www.naukri.com/job-listings-dev-101",
                link.ExternalAddress.ToString()
            );
        }

        [Fact]
        public async Task WritersRejectNulls()
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                new CsvPostingWriter().WriteAsync("", [Posting()], Ct)
            );
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                new CsvPostingWriter().WriteAsync("postings.csv", null!, Ct)
            );
            await Assert.ThrowsAsync<ArgumentException>(() =>
                new ExcelPostingWriter().WriteAsync("  ", [Posting()], Ct)
            );
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                new ExcelPostingWriter().WriteAsync("postings.xlsx", null!, Ct)
            );
        }
    }
}
