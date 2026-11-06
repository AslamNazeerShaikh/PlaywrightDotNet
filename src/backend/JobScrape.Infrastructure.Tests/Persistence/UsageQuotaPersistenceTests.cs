using JobScrape.Application.Abstractions;
using JobScrape.Domain.Access;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Persistence
{
    [Trait("Category", "Integration")]
    public sealed class UsageQuotaPersistenceTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task DefinitionRoundTripsEveryField()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using (var scope = database.Scope())
            {
                await scope.Get<IUsageQuotaRepository>()
                    .AddAsync(UsageQuota.Create(QuotaScope.Api, 10000, TimeSpan.FromHours(24)), Ct);
                await scope.Get<IUsageQuotaRepository>()
                    .AddAsync(UsageQuota.Create(QuotaScope.Mcp, 1000, TimeSpan.FromMinutes(30)), Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var quotas = reload.Get<IUsageQuotaRepository>();

            var api = await quotas.GetAsync(QuotaScope.Api, Ct);
            Assert.NotNull(api);
            Assert.Equal(10000, api.Limit);
            Assert.Equal(TimeSpan.FromHours(24), api.Window);

            var all = await quotas.ListAsync(Ct);
            Assert.Equal(2, all.Count);
            Assert.Equal(QuotaScope.Api, all[0].Scope);
            Assert.Equal(QuotaScope.Mcp, all[1].Scope);

            var mcp = await quotas.GetAsync(QuotaScope.Mcp, Ct);
            Assert.NotNull(mcp);
            mcp.SetLimit(5);
            await reload.Get<IUnitOfWork>().SaveChangesAsync(Ct);

            await using var reread = database.Scope();
            Assert.Equal(
                5,
                (await reread.Get<IUsageQuotaRepository>().GetAsync(QuotaScope.Mcp, Ct))!.Limit
            );
        }
    }
}
