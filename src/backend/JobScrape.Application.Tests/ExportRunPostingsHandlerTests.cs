using JobScrape.Application.Features.Runs;
using JobScrape.Application.Results;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class ExportRunPostingsHandlerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static RunResponse Run()
        {
            var profile = SearchProfile.Create(
                "backend-dev",
                SourceKey.Naukri,
                new SearchCriteria("c#", ["Pune"], null, null, false, []),
                new RunLimits(3, 60, 60, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(6)),
                Now
            );
            var run = ScrapeRun.Create(profile, Now);
            run.Start(Now);
            return RunResponse.From(run, profile.Name);
        }

        private static ExportPosting Posting(string externalId = "101", string? company = "Contoso")
        {
            return new ExportPosting(
                Guid.NewGuid(),
                externalId,
                "naukri",
                $"https://www.naukri.com/job-listings-dev-{externalId}",
                null,
                "Developer",
                company,
                "Pune",
                "Unknown",
                null,
                null,
                null,
                [],
                null,
                Now,
                Now
            );
        }

        [Fact]
        public async Task ExportsThroughTheMatchingWriter()
        {
            var run = Run();
            var handler = new ExportRunPostingsHandler(
                new FakeExportStore(run, [Posting("101"), Posting("102")]),
                [new FakeWriter(ExportFormat.Csv), new FakeWriter(ExportFormat.Xlsx)]
            );

            var result = await handler.HandleAsync(
                run.Id,
                ExportFormat.Csv,
                CompanyExportGroup.All,
                Ct
            );

            Assert.True(result.IsSuccess);
            var shortId = run.Id.ToString("N")[..8];
            Assert.Equal(
                $"naukri-backend-dev-20261102T080000Z-{shortId}-postings.csv",
                result.Value.FileName
            );
            Assert.Equal("text/csv", result.Value.ContentType);
            Assert.Contains("101", System.Text.Encoding.UTF8.GetString(result.Value.Content));
        }

        [Fact]
        public async Task XlsxNameCarriesTheXlsxExtension()
        {
            var run = Run();
            var handler = new ExportRunPostingsHandler(
                new FakeExportStore(run, [Posting("101")]),
                [new FakeWriter(ExportFormat.Xlsx)]
            );

            var result = await handler.HandleAsync(
                run.Id,
                ExportFormat.Xlsx,
                CompanyExportGroup.All,
                Ct
            );

            Assert.True(result.IsSuccess);
            var shortId = run.Id.ToString("N")[..8];
            Assert.Equal(
                $"naukri-backend-dev-20261102T080000Z-{shortId}-postings.xlsx",
                result.Value.FileName
            );
        }

        [Fact]
        public async Task MissingProfileFallsBackToProfileSlug()
        {
            var seeded = Run() with { ProfileName = null };
            var handler = new ExportRunPostingsHandler(
                new FakeExportStore(seeded, [Posting("101")]),
                [new FakeWriter(ExportFormat.Csv)]
            );

            var result = await handler.HandleAsync(
                seeded.Id,
                ExportFormat.Csv,
                CompanyExportGroup.All,
                Ct
            );

            Assert.True(result.IsSuccess);
            var shortId = seeded.Id.ToString("N")[..8];
            Assert.Equal(
                $"naukri-profile-20261102T080000Z-{shortId}-postings.csv",
                result.Value.FileName
            );
        }

        [Fact]
        public async Task OnlyCompaniesKeepsMatchingPostings()
        {
            var run = Run() with { Companies = ["Contoso"] };
            var handler = new ExportRunPostingsHandler(
                new FakeExportStore(run, [Posting("101", "Contoso"), Posting("102", "Fabrikam")]),
                [new FakeWriter(ExportFormat.Csv)]
            );

            var result = await handler.HandleAsync(
                run.Id,
                ExportFormat.Csv,
                CompanyExportGroup.Only,
                Ct
            );

            Assert.True(result.IsSuccess);
            Assert.EndsWith("-only-companies.csv", result.Value.FileName, StringComparison.Ordinal);
            var text = System.Text.Encoding.UTF8.GetString(result.Value.Content);
            Assert.Contains("101", text, StringComparison.Ordinal);
            Assert.DoesNotContain("102", text, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ExcludeCompaniesDropsMatchingPostings()
        {
            var run = Run() with { Companies = ["Contoso"] };
            var handler = new ExportRunPostingsHandler(
                new FakeExportStore(run, [Posting("101", "Contoso"), Posting("102", "Fabrikam")]),
                [new FakeWriter(ExportFormat.Csv)]
            );

            var result = await handler.HandleAsync(
                run.Id,
                ExportFormat.Csv,
                CompanyExportGroup.Exclude,
                Ct
            );

            Assert.True(result.IsSuccess);
            Assert.EndsWith(
                "-exclude-companies.csv",
                result.Value.FileName,
                StringComparison.Ordinal
            );
            var text = System.Text.Encoding.UTF8.GetString(result.Value.Content);
            Assert.DoesNotContain("101", text, StringComparison.Ordinal);
            Assert.Contains("102", text, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CompanySliceWithoutACompanyListIsAValidationFailure()
        {
            var run = Run();
            var handler = new ExportRunPostingsHandler(
                new FakeExportStore(run, [Posting("101")]),
                [new FakeWriter(ExportFormat.Csv)]
            );

            var result = await handler.HandleAsync(
                run.Id,
                ExportFormat.Csv,
                CompanyExportGroup.Only,
                Ct
            );

            Assert.True(result.IsFailure);
            Assert.Equal("EXPORT_COMPANIES_MISSING", result.Error.Code);
        }

        [Fact]
        public void ConstructorsRejectNulls()
        {
            var store = new FakeExportStore(null, []);
            var writers = new IPostingExportWriter[] { new FakeWriter(ExportFormat.Csv) };
            Assert.Throws<ArgumentNullException>(() =>
                new ExportRunPostingsHandler(null!, writers)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new ExportRunPostingsHandler(store, (IEnumerable<IPostingExportWriter>)null!)
            );
        }

        [Fact]
        public async Task EmptyIdFailsValidationBeforeAnyRead()
        {
            var store = new FakeExportStore(null, []);
            var handler = new ExportRunPostingsHandler(store, [new FakeWriter(ExportFormat.Csv)]);

            var result = await handler.HandleAsync(
                Guid.Empty,
                ExportFormat.Csv,
                CompanyExportGroup.All,
                Ct
            );

            Assert.False(result.IsSuccess);
            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
            Assert.Equal(0, store.Reads);
        }

        [Fact]
        public async Task MissingRunIsNotFound()
        {
            var handler = new ExportRunPostingsHandler(
                new FakeExportStore(null, []),
                [new FakeWriter(ExportFormat.Csv)]
            );

            var result = await handler.HandleAsync(
                Guid.NewGuid(),
                ExportFormat.Csv,
                CompanyExportGroup.All,
                Ct
            );

            Assert.False(result.IsSuccess);
            Assert.Equal("RUN_NOT_FOUND", result.Error.Code);
        }

        [Fact]
        public async Task MissingPostingsAreNotFound()
        {
            var run = Run();
            var handler = new ExportRunPostingsHandler(
                new FakeExportStore(run, null),
                [new FakeWriter(ExportFormat.Csv)]
            );

            var result = await handler.HandleAsync(
                run.Id,
                ExportFormat.Csv,
                CompanyExportGroup.All,
                Ct
            );

            Assert.False(result.IsSuccess);
            Assert.Equal("RUN_NOT_FOUND", result.Error.Code);
        }

        [Fact]
        public async Task MissingWriterIsUnsupportedFormat()
        {
            var run = Run();
            var handler = new ExportRunPostingsHandler(
                new FakeExportStore(run, [Posting()]),
                [new FakeWriter(ExportFormat.Csv)]
            );

            var result = await handler.HandleAsync(
                run.Id,
                ExportFormat.Xlsx,
                CompanyExportGroup.All,
                Ct
            );

            Assert.False(result.IsSuccess);
            Assert.Equal("EXPORT_FORMAT_UNSUPPORTED", result.Error.Code);
        }

        private sealed class FakeExportStore(
            RunResponse? run,
            IReadOnlyList<ExportPosting>? postings
        ) : IRunStore
        {
            public int Reads { get; private set; }

            public Task<RunResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
            {
                return Task.FromResult(run?.Id == id ? run : null);
            }

            public Task<RunListResponse> ListAsync(
                int offset,
                int limit,
                CancellationToken cancellationToken
            )
            {
                throw new NotImplementedException();
            }

            public Task<RunPostingListResponse?> ListPostingsAsync(
                Guid runId,
                int offset,
                int limit,
                CancellationToken cancellationToken
            )
            {
                throw new NotImplementedException();
            }

            public Task<IReadOnlyList<ExportPosting>?> ListExportPostingsAsync(
                Guid runId,
                int limit,
                CancellationToken cancellationToken
            )
            {
                Reads++;
                return Task.FromResult(postings);
            }
        }

        private sealed class FakeWriter(ExportFormat format) : IPostingExportWriter
        {
            public ExportFormat Format { get; } = format;

            public Task<ExportFile> WriteAsync(
                string fileName,
                IReadOnlyList<ExportPosting> postings,
                CancellationToken cancellationToken
            )
            {
                var text = string.Join('\n', postings.Select(p => p.ExternalJobId));
                return Task.FromResult(
                    new ExportFile(fileName, "text/csv", System.Text.Encoding.UTF8.GetBytes(text))
                );
            }
        }
    }
}
