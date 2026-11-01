using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using Xunit;

namespace JobScrape.Application.Tests.Scraping
{
    public sealed class SourceCatalogTests
    {
        private static SourceSettings Settings(SourceKey source)
        {
            return new SourceSettings(source, true, 25, 60, 2, TimeSpan.FromMinutes(45));
        }

        [Fact]
        public void RegistryResolvesByKeyAndReturnsNullWhenMissing()
        {
            var naukri = new StubSource(SourceKey.Naukri);
            var registry = new JobSourceRegistry([naukri]);

            Assert.Same(naukri, registry.Find(SourceKey.Naukri));
            Assert.Null(registry.Find(SourceKey.LinkedIn));
        }

        [Fact]
        public void RegistryRejectsDuplicateAndNullArguments()
        {
            Assert.Throws<ArgumentException>(() =>
                new JobSourceRegistry([
                    new StubSource(SourceKey.Naukri),
                    new StubSource(SourceKey.Naukri),
                ])
            );
            Assert.Throws<ArgumentNullException>(() => new JobSourceRegistry(null!));
            Assert.Throws<ArgumentNullException>(() => new JobSourceRegistry([]).Find(null!));
        }

        [Fact]
        public void CatalogResolvesByKeyAndReturnsNullWhenMissing()
        {
            var catalog = new SourceSettingsCatalog([Settings(SourceKey.Naukri)]);

            Assert.Equal(Settings(SourceKey.Naukri), catalog.Find(SourceKey.Naukri));
            Assert.Null(catalog.Find(SourceKey.Indeed));
        }

        [Fact]
        public void CatalogRejectsDuplicateAndNullArguments()
        {
            Assert.Throws<ArgumentException>(() =>
                new SourceSettingsCatalog([Settings(SourceKey.Naukri), Settings(SourceKey.Naukri)])
            );
            Assert.Throws<ArgumentNullException>(() => new SourceSettingsCatalog(null!));
            Assert.Throws<ArgumentNullException>(() => new SourceSettingsCatalog([]).Find(null!));
        }

        [Fact]
        public void BlockedPageCarriesOnlyItsClassification()
        {
            var captcha = new ChallengeAssessment(ChallengeKind.Captcha, 1, "title=verify");

            var page = SearchPageResult.Blocked(captcha);

            Assert.Empty(page.Jobs);
            Assert.Null(page.NextCursor);
            Assert.Null(page.Fingerprint);
            Assert.Same(captcha, page.Challenge);
        }

        private sealed class StubSource : IJobSource
        {
            public StubSource(SourceKey key)
            {
                Key = key;
            }

            public SourceKey Key { get; }

            public SourceCapabilities Capabilities { get; } = new(SupportsDetails: false);

            public Task<SearchPageResult> SearchPageAsync(
                SourceRunContext context,
                SearchPageRequest request,
                CancellationToken cancellationToken
            )
            {
                throw new NotSupportedException();
            }

            public Task<JobDetailResult> GetDetailAsync(
                SourceRunContext context,
                JobReference job,
                CancellationToken cancellationToken
            )
            {
                throw new NotSupportedException();
            }
        }
    }
}
