using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class ScrapeRunProgressTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        [Fact]
        public void StartHasNothingRecorded()
        {
            var progress = ScrapeRunProgress.Start(ScrapeRunId.New(), Now);

            Assert.Equal(0, progress.PagesCompleted);
            Assert.Null(progress.NextCursor);
            Assert.Empty(progress.SeenExternalIds);
            Assert.Equal(Now, progress.UpdatedAt);
        }

        [Fact]
        public void RecordPageAdvancesAndReplaces()
        {
            var progress = ScrapeRunProgress.Start(ScrapeRunId.New(), Now);

            progress.RecordPage(1, "next", ["101"], Now);
            progress.RecordPage(2, null, ["101", "102"], Now.AddMinutes(1));

            Assert.Equal(2, progress.PagesCompleted);
            Assert.Null(progress.NextCursor);
            Assert.Equal(["101", "102"], progress.SeenExternalIds);
            Assert.Equal(Now.AddMinutes(1), progress.UpdatedAt);
        }

        [Fact]
        public void RecordPageRefusesBackwardPages()
        {
            var progress = ScrapeRunProgress.Start(ScrapeRunId.New(), Now);
            progress.RecordPage(2, null, ["101"], Now);

            Assert.Throws<DomainException>(() => progress.RecordPage(1, null, ["101"], Now));
        }

        [Fact]
        public void RecordPageRejectsNullIds()
        {
            var progress = ScrapeRunProgress.Start(ScrapeRunId.New(), Now);

            Assert.Throws<ArgumentNullException>(() =>
                progress.RecordPage(1, null, null!, Now)
            );
        }
    }
}
