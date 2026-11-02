using System.Net;
using AngleSharp.Html.Parser;
using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources.Indeed
{
    internal static class IndeedTestPages
    {
        public static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

        public const string PageUrl =
            "https://in.indeed.com/jobs?q=dotnet&l=Pune%2C+Maharashtra&fromage=7";

        public static string Fixture(string name)
        {
            return File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Sources", "Indeed", "Fixtures", name)
            );
        }

        public static (string Title, string BodyText) Visible(string html)
        {
            using var document = new HtmlParser().ParseDocument(html);
            return (document.Title ?? string.Empty, document.Body?.TextContent ?? string.Empty);
        }

        // Mirrors RunScrapeHandler.UpsertAsync: a snapshot is stored only if
        // the aggregate accepts it.
        public static JobPosting AssertAccepted(JobSnapshot snapshot)
        {
            var posting = JobPosting.Create(snapshot, Now);
            Assert.Equal(snapshot.ExternalJobId, posting.ExternalJobId.Value);
            return posting;
        }

        public static string Card(
            string key = "abc123def000XYZa",
            string? title = "Senior .NET Developer",
            string? href = null,
            string? salary = null,
            string? location = "Pune, Maharashtra",
            string? posted = "Posted3 days ago",
            string? company = "Contoso",
            string wrapperAttributes = ""
        )
        {
            href ??= $"https://in.indeed.com/viewjob?jk={key}";
            var titleHtml = title is null
                ? string.Empty
                : $"<h2 class=\"jobTitle\"><a href=\"{Encode(href)}\">{Encode(title)}</a></h2>";
            var salaryHtml = salary is null
                ? string.Empty
                : $"<div data-testid=\"attribute_snippet_testid\">{Encode(salary)}</div>";
            var locationHtml = location is null
                ? string.Empty
                : $"<div data-testid=\"text-location\">{Encode(location)}</div>";
            var companyHtml = company is null
                ? string.Empty
                : $"<span data-testid=\"company-name\">{Encode(company)}</span>";
            var postedHtml = posted is null
                ? string.Empty
                : $"<span class=\"date\">{Encode(posted)}</span>";
            return $"""
                <div class="job_seen_beacon" data-jk="{Encode(key)}"{wrapperAttributes}>
                {titleHtml}{companyHtml}{locationHtml}{salaryHtml}{postedHtml}
                <div data-testid="jobsnippet_footer">Build services.</div>
                </div>
                """;
        }

        public static string Page(params string[] cards)
        {
            return $"""
                <html><head><title>Jobs - Indeed</title></head><body>
                <div class="jobsearch-JobCountAndSortPane-jobCount">1&ndash;10 of 200 jobs</div>
                {string.Concat(cards)}
                <a href="/jobs?q=dotnet&start=10">Next</a>
                </body></html>
                """;
        }

        private static string Encode(string value)
        {
            return WebUtility.HtmlEncode(value);
        }
    }
}
