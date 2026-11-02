using JobScrape.Application.Features.Runs;
using JobScrape.Application.Results;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class ExportRunPostingsHandlerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static ExportPosting Posting(string externalId = "101")
        {
            return new ExportPosting(
                Guid.NewGuid(),
                externalId,
                "naukri",
                $"https://www.naukri.com/job-listings-dev-{externalId}",
                null,
                "Developer",
                "Contoso",
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
        public void ConstructorsRejectNulls()
        {
            var store = new FakeExportStore([]);
            var writers = new IPostingExportWriter[] { new FakeWriter(ExportFormat.Csv) };
            Assert.Throws<ArgumentNullException>(() => new ExportRunPostingsHandler(null!, writers));
            Assert.Throws<ArgumentNullException>(
                () => new ExportRunPostingsHandler(store, (IEnumerable<IPostingExportWriter>)null!)
            );
        }

        [Fact]
        public async Task ExportsThroughTheMatchingWriter()
        {
            var runId = Guid.NewGuid();
            var handler = new ExportRunPostingsHandler(
                new FakeExportStore([Posting("101"), Posting("102")]),
                [new FakeWriter(ExportFormat.Csv), new FakeWriter(ExportFormat.Xlsx)]
            );

            var result = await handler.HandleAsync(runId, ExportFormat.Csv, Ct);

            Assert.True(result.IsSuccess);
            Assert.Equal($"run-{runId}-postings.csv", result.Value.FileName);
            Assert.Equal("text/csv", result.Value.ContentType);
            Assert.Contains("101", System.Text.Encoding.UTF8.GetString(result.Value.Content));
        }

        [Fact]
        public async Task EmptyIdFailsValidationBeforeAnyRead()
        {
            var store = new FakeExportStore([]);
            var handler = new ExportRunPostingsHandler(
                store,
                [new FakeWriter(ExportFormat.Csv)]
            );

            var result = await handler.HandleAsync(Guid.Empty, ExportFormat.Csv, Ct);

            Assert.False(result.IsSuccess);
            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
            Assert.Equal(0, store.Reads);
        }

        [Fact]
        public async Task MissingRunIsNotFound()
        {
            var handler = new ExportRunPostingsHandler(
                new FakeExportStore([], exists: false),
                [new FakeWriter(ExportFormat.Csv)]
            );

            var result = await handler.HandleAsync(Guid.NewGuid(), ExportFormat.Csv, Ct);

            Assert.False(result.IsSuccess);
            Assert.Equal("RUN_NOT_FOUND", result.Error.Code);
        }

        [Fact]
        public async Task MissingWriterIsUnsupportedFormat()
        {
            var runId = Guid.NewGuid();
            var handler = new ExportRunPostingsHandler(
                new FakeExportStore([Posting()]),
                [new FakeWriter(ExportFormat.Csv)]
            );

            var result = await handler.HandleAsync(runId, ExportFormat.Xlsx, Ct);

            Assert.False(result.IsSuccess);
            Assert.Equal("EXPORT_FORMAT_UNSUPPORTED", result.Error.Code);
        }

        private sealed class FakeExportStore(IReadOnlyList<ExportPosting> postings, bool exists = true)
            : IRunStore
        {
            public int Reads { get; private set; }

            public Task<RunResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<RunListResponse> ListAsync(int limit, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<RunPostingListResponse?> ListPostingsAsync(
                Guid runId,
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
                IReadOnlyList<ExportPosting>? items = exists ? postings : null;
                return Task.FromResult(items);
            }
        }

        private sealed class FakeWriter(ExportFormat format) : IPostingExportWriter
        {
            public ExportFormat Format { get; } = format;

            public Task<ExportFile> WriteAsync(
                Guid runId,
                IReadOnlyList<ExportPosting> postings,
                CancellationToken cancellationToken
            )
            {
                var text = string.Join('\n', postings.Select(p => p.ExternalJobId));
                return Task.FromResult(
                    new ExportFile(
                        $"run-{runId}-postings.csv",
                        "text/csv",
                        System.Text.Encoding.UTF8.GetBytes(text)
                    )
                );
            }
        }
    }
}
