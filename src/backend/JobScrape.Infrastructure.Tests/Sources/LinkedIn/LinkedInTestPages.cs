using System.Net;
using AngleSharp.Html.Parser;
using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources.LinkedIn
{
    internal static class LinkedInTestPages
    {
        public static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

        public const string PageUrl =
            "https://www.linkedin.com/jobs/search?keywords=dotnet&location=Pune&f_TPR=r604800";

        public static string Fixture(string name)
        {
            return File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Sources", "LinkedIn", "Fixtures", name)
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
            string id = "4012345670",
            string? title = "Senior .NET Developer",
            string? href = null,
            string? location = "Pune, Maharashtra, India",
            string? posted = "3 days ago",
            string? company = "Contoso",
            string wrapperAttributes = ""
        )
        {
            href ??= $"https://www.linkedin.com/jobs/view/{id}/";
            var titleHtml = title is null
                ? string.Empty
                : $"<a class=\"job-card-container__link\" title=\"{Encode(title)}\" href=\"{Encode(href)}\">{Encode(title)}</a>";
            var locationHtml = location is null
                ? string.Empty
                : $"<ul><li class=\"job-card-container__metadata-item\">{Encode(location)}</li></ul>";
            var companyHtml = company is null
                ? string.Empty
                : $"<span class=\"job-card-container__company-name\">{Encode(company)}</span>";
            var postedHtml = posted is null ? string.Empty : $"<time>{Encode(posted)}</time>";
            return $"""
                <li class="jobs-search-results__list-item" data-job-id="{Encode(id)}"{wrapperAttributes}>
                {titleHtml}{companyHtml}{locationHtml}{postedHtml}
                </li>
                """;
        }

        public static string Page(params string[] cards)
        {
            return $"""
                <html><head><title>Jobs - LinkedIn</title></head><body>
                <div class="jobs-search-results-list__subtitle">1-{cards.Length} of 100 jobs</div>
                <ul class="jobs-search-results__list">{string.Concat(cards)}</ul>
                <a href="/jobs/search?keywords=dotnet&start=25">Next</a>
                </body></html>
                """;
        }

        private static string Encode(string value)
        {
            return WebUtility.HtmlEncode(value);
        }
    }
}
