using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using Xunit;

namespace JobScrape.Domain.Tests.Access
{
    public sealed class SourceQuotaTests
    {
        // 23:30 in India (UTC+05:30) is 18:00 UTC the same day.
        private static readonly DateTimeOffset Now = new(
            2026,
            11,
            2,
            23,
            30,
            0,
            TimeSpan.FromHours(5.5)
        );

        [Fact]
        public void StartDayUsesTheUtcDateAndZeroCounts()
        {
            var quota = SourceQuota.StartDay(SourceKey.Naukri, Now);

            Assert.Same(SourceKey.Naukri, quota.Source);
            Assert.Equal(new DateOnly(2026, 11, 2), quota.QuotaDate);
            Assert.Equal(0, quota.PagesFetched);
            Assert.Equal(0, quota.DetailsFetched);
            Assert.Equal(0, quota.RunsStarted);
            Assert.Equal(0, quota.Challenges);
            Assert.Equal(Now, quota.UpdatedAt);
        }

        [Fact]
        public void StartDayUsesUtcNotLocalDate()
        {
            // 02:00 in India on Nov 3 is still Nov 2 in UTC.
            var quota = SourceQuota.StartDay(
                SourceKey.Naukri,
                new DateTimeOffset(2026, 11, 3, 2, 0, 0, TimeSpan.FromHours(5.5))
            );

            Assert.Equal(new DateOnly(2026, 11, 2), quota.QuotaDate);
        }

        [Fact]
        public void PagesAreConsumedUntilCapThenExceededWithoutConsuming()
        {
            var quota = SourceQuota.StartDay(SourceKey.Naukri, Now);

            var first = quota.TryConsumePage(2, Now);
            var second = quota.TryConsumePage(2, Now.AddMinutes(1));
            var third = quota.TryConsumePage(2, Now.AddMinutes(2));

            Assert.Equal(new QuotaDecision(true, QuotaKind.Pages, 2, 1), first);
            Assert.Equal(new QuotaDecision(true, QuotaKind.Pages, 2, 2), second);
            Assert.Equal(new QuotaDecision(false, QuotaKind.Pages, 2, 2), third);
            Assert.Equal(2, quota.PagesFetched);
            Assert.Equal(Now.AddMinutes(1), quota.UpdatedAt);
        }

        [Fact]
        public void DetailsAndRunsHaveTheirOwnCounters()
        {
            var quota = SourceQuota.StartDay(SourceKey.Indeed, Now);

            Assert.True(quota.TryConsumeDetail(1, Now).IsAllowed);
            Assert.False(quota.TryConsumeDetail(1, Now).IsAllowed);
            Assert.True(quota.TryStartRun(1, Now).IsAllowed);
            var exceeded = quota.TryStartRun(1, Now);

            Assert.Equal(new QuotaDecision(false, QuotaKind.Runs, 1, 1), exceeded);
            Assert.Equal(1, quota.DetailsFetched);
            Assert.Equal(1, quota.RunsStarted);
            Assert.Equal(0, quota.PagesFetched);
        }

        [Fact]
        public void ZeroCapBlocksEverything()
        {
            var quota = SourceQuota.StartDay(SourceKey.LinkedIn, Now);

            Assert.False(quota.TryConsumePage(0, Now).IsAllowed);
            Assert.False(quota.TryConsumeDetail(0, Now).IsAllowed);
            Assert.False(quota.TryStartRun(0, Now).IsAllowed);
        }

        [Fact]
        public void NegativeCapIsAnInvariantViolation()
        {
            var quota = SourceQuota.StartDay(SourceKey.Naukri, Now);

            Assert.Throws<DomainException>(() => quota.TryConsumePage(-1, Now));
        }

        [Fact]
        public void ChallengesAreCountedNotCapped()
        {
            var quota = SourceQuota.StartDay(SourceKey.Naukri, Now);

            quota.RecordChallenge(Now);
            quota.RecordChallenge(Now.AddMinutes(1));

            Assert.Equal(2, quota.Challenges);
            Assert.Equal(Now.AddMinutes(1), quota.UpdatedAt);
        }

        [Fact]
        public void ChargingAnotherUtcDayIsRejected()
        {
            var quota = SourceQuota.StartDay(SourceKey.Naukri, Now);
            var tomorrowUtc = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

            Assert.Throws<DomainException>(() => quota.TryConsumePage(5, tomorrowUtc));
            Assert.Throws<DomainException>(() => quota.TryConsumeDetail(5, tomorrowUtc));
            Assert.Throws<DomainException>(() => quota.TryStartRun(5, tomorrowUtc));
            Assert.Throws<DomainException>(() => quota.RecordChallenge(tomorrowUtc));
            Assert.Equal(0, quota.PagesFetched);
        }

        [Fact]
        public void StartDayRejectsNullSource()
        {
            Assert.Throws<ArgumentNullException>(() => SourceQuota.StartDay(null!, Now));
        }
    }
}
