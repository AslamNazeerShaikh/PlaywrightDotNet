using System.Globalization;
using JobScrape.Domain;
using JobScrape.Domain.Access;
using Xunit;

namespace JobScrape.Domain.Tests.Access
{
    public sealed class UsageQuotaTests
    {
        [Fact]
        public void CreateStoresScopeLimitAndWindow()
        {
            var quota = UsageQuota.Create(QuotaScope.Api, 100, TimeSpan.FromHours(1));

            Assert.Equal(QuotaScope.Api, quota.Scope);
            Assert.Equal(100, quota.Limit);
            Assert.Equal(TimeSpan.FromHours(1), quota.Window);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void CreateRejectsNonPositiveLimit(int limit)
        {
            Assert.Throws<DomainException>(
                () => UsageQuota.Create(QuotaScope.Api, limit, TimeSpan.FromMinutes(1))
            );
        }

        [Theory]
        [InlineData("00:00:00")]
        [InlineData("-00:01:00")]
        [InlineData("1.00:00:01")]
        public void CreateRejectsOutOfRangeWindow(string window)
        {
            Assert.Throws<DomainException>(
                () => UsageQuota.Create(QuotaScope.Mcp, 10, TimeSpan.Parse(window, CultureInfo.InvariantCulture))
            );
        }

        [Fact]
        public void CreateAcceptsExactly24Hours()
        {
            var quota = UsageQuota.Create(QuotaScope.Mcp, 10, TimeSpan.FromHours(24));

            Assert.Equal(TimeSpan.FromHours(24), quota.Window);
        }

        [Fact]
        public void SetLimitChangesTheDefinition()
        {
            var quota = UsageQuota.Create(QuotaScope.Api, 100, TimeSpan.FromHours(1));

            quota.SetLimit(5);

            Assert.Equal(5, quota.Limit);
        }

        [Fact]
        public void SetLimitRejectsNonPositiveLimit()
        {
            var quota = UsageQuota.Create(QuotaScope.Api, 100, TimeSpan.FromHours(1));

            Assert.Throws<DomainException>(() => quota.SetLimit(0));
            Assert.Equal(100, quota.Limit);
        }

        [Fact]
        public void SetWindowChangesTheDefinition()
        {
            var quota = UsageQuota.Create(QuotaScope.Api, 100, TimeSpan.FromHours(1));

            quota.SetWindow(TimeSpan.FromMinutes(5));

            Assert.Equal(TimeSpan.FromMinutes(5), quota.Window);
        }

        [Fact]
        public void SetWindowRejectsOutOfRangeWindow()
        {
            var quota = UsageQuota.Create(QuotaScope.Api, 100, TimeSpan.FromHours(1));

            Assert.Throws<DomainException>(() => quota.SetWindow(TimeSpan.FromHours(25)));
            Assert.Equal(TimeSpan.FromHours(1), quota.Window);
        }

        [Fact]
        public void DefaultsShipApiAndMcpBudgets()
        {
            var defaults = UsageQuotaDefaults.All();

            Assert.Equal(2, defaults.Count);
            var api = Assert.Single(defaults, quota => quota.Scope == QuotaScope.Api);
            var mcp = Assert.Single(defaults, quota => quota.Scope == QuotaScope.Mcp);
            Assert.Equal(10000, api.Limit);
            Assert.Equal(TimeSpan.FromHours(24), api.Window);
            Assert.Equal(1000, mcp.Limit);
            Assert.Equal(TimeSpan.FromHours(24), mcp.Window);
        }
    }
}
