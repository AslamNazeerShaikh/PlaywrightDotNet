using JobScrape.Application.Results;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class ScrapeErrorsTests
    {
        private static readonly Guid Id = new("6f9619ff-8b86-d011-b42d-00c04fc964ff");

        private static readonly DateTimeOffset Until = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);

        public static TheoryData<ErrorInfo, string, ErrorKind> Cases =>
            new()
            {
                { Errors.ProfileDisabled(Id), "PROFILE_DISABLED", ErrorKind.Conflict },
                {
                    Errors.BrowserProfileNotFound(Id),
                    "BROWSER_PROFILE_NOT_FOUND",
                    ErrorKind.NotFound
                },
                {
                    Errors.SourceUnavailable(SourceKey.Naukri),
                    "SOURCE_UNAVAILABLE",
                    ErrorKind.Unavailable
                },
                { Errors.SourceDisabled(SourceKey.Indeed), "SOURCE_DISABLED", ErrorKind.Forbidden },
                {
                    Errors.ComplianceBlocked(SourceKey.LinkedIn, "needs consent"),
                    "COMPLIANCE_BLOCKED",
                    ErrorKind.Forbidden
                },
                {
                    Errors.QuotaExceeded(
                        SourceKey.Naukri,
                        new QuotaDecision(false, QuotaKind.Runs, 2, 2)
                    ),
                    "QUOTA_EXCEEDED",
                    ErrorKind.QuotaExceeded
                },
                {
                    Errors.SourceCoolingDown(SourceKey.Naukri, Until),
                    "SOURCE_COOLING_DOWN",
                    ErrorKind.CoolingDown
                },
                {
                    Errors.SessionUnavailable(Id, resetRequired: false, Until),
                    "SESSION_COOLING_DOWN",
                    ErrorKind.CoolingDown
                },
                {
                    Errors.SessionUnavailable(Id, resetRequired: true, null),
                    "SESSION_RESET_REQUIRED",
                    ErrorKind.CoolingDown
                },
            };

        [Theory]
        [MemberData(nameof(Cases))]
        public void EachFactoryHasStableCodeKindAndSafeText(
            ErrorInfo error,
            string code,
            ErrorKind kind
        )
        {
            Assert.Equal(code, error.Code);
            Assert.Equal(kind, error.Kind);
            Assert.False(string.IsNullOrWhiteSpace(error.Title));
            Assert.False(string.IsNullOrWhiteSpace(error.Detail));
            Assert.NotNull(error.Metadata);
        }

        [Fact]
        public void MetadataCarriesIdsCountsAndRetryHints()
        {
            var quota = Errors.QuotaExceeded(
                SourceKey.Naukri,
                new QuotaDecision(false, QuotaKind.Pages, 25, 25)
            );
            var cooling = Errors.SourceCoolingDown(SourceKey.Naukri, Until);
            var blocked = Errors.ComplianceBlocked(SourceKey.LinkedIn, "needs consent");

            Assert.Equal(25, quota.Metadata!["limit"]);
            Assert.Equal("Pages", quota.Metadata["quota"]);
            Assert.Contains("daily pages cap (25)", quota.Detail, StringComparison.Ordinal);
            Assert.Equal(Until, cooling.Metadata!["retryAfter"]);
            Assert.Equal("needs consent", blocked.Detail);
            Assert.Equal("HighRisk", blocked.Metadata!["tier"]);
        }

        [Fact]
        public void FactoriesRejectNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => Errors.SourceUnavailable(null!));
            Assert.Throws<ArgumentNullException>(() => Errors.SourceDisabled(null!));
            Assert.Throws<ArgumentNullException>(() => Errors.ComplianceBlocked(null!, "r"));
            Assert.Throws<ArgumentNullException>(() =>
                Errors.ComplianceBlocked(SourceKey.Naukri, null!)
            );
            Assert.Throws<ArgumentNullException>(() =>
                Errors.QuotaExceeded(null!, new QuotaDecision(false, QuotaKind.Runs, 1, 1))
            );
            Assert.Throws<ArgumentNullException>(() =>
                Errors.QuotaExceeded(SourceKey.Naukri, null!)
            );
            Assert.Throws<ArgumentNullException>(() => Errors.SourceCoolingDown(null!, null));
        }
    }
}
