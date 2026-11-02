using System.Net;
using AngleSharp.Html.Parser;
using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources.Naukri
{
    internal static class NaukriTestPages
    {
        // The fixtures were modelled on markup captured on this date.
        public static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

        public static string Fixture(string name)
        {
            return File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Sources", "Naukri", "Fixtures", name)
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
            string id = "011126000501",
            string? title = "Senior .NET Developer",
            string? href = null,
            string? experience = "5-8 Yrs",
            string? salary = null,
            string? location = "Pune",
            string? posted = "2 days ago",
            string? company = "Contoso",
            string[]? skills = null,
            string wrapperAttributes = ""
        )
        {
            href ??= $"https://www.naukri.com/job-listings-senior-dotnet-developer-contoso-{id}";
            var titleHtml = title is null
                ? string.Empty
                : $"<h2><a class=\"title \" title=\"{Encode(title)}\" href=\"{Encode(href)}\">{Encode(title)}</a></h2>";
            var salaryHtml = salary is null
                ? string.Empty
                : $"<span class=\"sal-wrap ver-line\"><span class=\"sal\"><span title=\"{Encode(salary)} \" class=\"\">{Encode(salary)}</span></span></span>";
            var experienceHtml = experience is null
                ? string.Empty
                : $"<span class=\"exp-wrap\"><span class=\"exp\"><span class=\"expwdth\">{Encode(experience)}</span></span></span>";
            var locationHtml = location is null
                ? string.Empty
                : $"<span class=\"loc-wrap ver-line\"><span class=\"loc\"><span class=\"locWdth\">{Encode(location)}</span></span></span>";
            var companyHtml = company is null
                ? string.Empty
                : $"<span class=\" comp-dtls-wrap\"><a class=\" comp-name mw-25\" href=\"https://www.naukri.com/contoso-jobs-careers-1\">{Encode(company)}</a></span>";
            var postedHtml = posted is null
                ? string.Empty
                : $"<span class=\"job-post-day \">{Encode(posted)}</span>";
            var tags = string.Concat(
                (skills ?? ["C#"]).Select(skill =>
                    $"<li class=\"dot-gt tag-li \">{Encode(skill)}</li>"
                )
            );
            var idAttribute = id.Length == 0 ? string.Empty : $" data-job-id=\"{Encode(id)}\"";
            return $"""
                <li><div class="srp-jobtuple-wrapper"{idAttribute}{wrapperAttributes}><div class="cust-job-tuple layout-wrapper lay-2 sjw__tuple ">
                <div class=" row1">{titleHtml}</div>
                <div class=" row2">{companyHtml}</div>
                <div class=" row3"><div class="job-details ">{experienceHtml}{salaryHtml}{locationHtml}</div></div>
                <div class=" row4"><span class="job-desc">Build services.</span></div>
                <div class=" row5"><ul class="tags-gt ">{tags}</ul></div>
                <div class=" row6">{postedHtml}</div>
                </div></div></li>
                """;
        }

        public static string Page(params string[] cards)
        {
            return $"""
                <html><head><title>Jobs - Naukri.com</title></head><body>
                <span class="styles_count-string__x">1 - {cards.Length} of {cards.Length}</span>
                <ul aria-label="Job search results">{string.Concat(cards)}</ul>
                <a aria-disabled="true"><span>Next</span></a>
                </body></html>
                """;
        }

        private static string Encode(string value)
        {
            return WebUtility.HtmlEncode(value);
        }
    }
}
