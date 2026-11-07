using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Quotas;
using JobScrape.Application.Results;
using JobScrape.Domain.Access;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class QuotaHandlerTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private sealed class FakeQuotas : IUsageQuotaRepository
        {
            private readonly List<UsageQuota> _quotas = [];

            public int Adds { get; private set; }

            public void Seed(UsageQuota quota)
            {
                _quotas.Add(quota);
            }

            public Task<UsageQuota?> GetAsync(QuotaScope scope, CancellationToken cancellationToken)
            {
                return Task.FromResult(_quotas.FirstOrDefault(quota => quota.Scope == scope));
            }

            public Task<IReadOnlyList<UsageQuota>> ListAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<UsageQuota>>(
                    _quotas.OrderBy(quota => quota.Scope).ToList()
                );
            }

            public Task AddAsync(UsageQuota quota, CancellationToken cancellationToken)
            {
                Adds++;
                _quotas.Add(quota);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public int Saves { get; private set; }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
            {
                Saves++;
                return Task.FromResult(0);
            }

            public bool IsUniqueViolation(Exception exception)
            {
                return false;
            }
        }

        [Fact]
        public async Task ListMapsEveryDefinition()
        {
            var quotas = new FakeQuotas();
            quotas.Seed(UsageQuota.Create(QuotaScope.Api, 100, TimeSpan.FromHours(1)));
            var handler = new ListUsageQuotasHandler(quotas);

            var response = await handler.HandleAsync(Ct);

            var single = Assert.Single(response.Quotas);
            Assert.Equal("Api", single.Scope);
            Assert.Equal(100, single.Limit);
            Assert.Equal(TimeSpan.FromHours(1), single.Window);
        }

        [Fact]
        public async Task UpdateChangesAnExistingDefinition()
        {
            var quotas = new FakeQuotas();
            quotas.Seed(UsageQuota.Create(QuotaScope.Mcp, 100, TimeSpan.FromHours(1)));
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateUsageQuotaHandler(quotas, unitOfWork);

            var result = await handler.HandleAsync("Mcp", 5, TimeSpan.FromMinutes(2), Ct);

            Assert.True(result.IsSuccess);
            Assert.Equal("Mcp", result.Value.Scope);
            Assert.Equal(5, result.Value.Limit);
            Assert.Equal(TimeSpan.FromMinutes(2), result.Value.Window);
            Assert.Equal(0, quotas.Adds);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task UpdateCreatesAMissingDefinition()
        {
            var quotas = new FakeQuotas();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateUsageQuotaHandler(quotas, unitOfWork);

            var result = await handler.HandleAsync("Api", 7, TimeSpan.FromMinutes(3), Ct);

            Assert.True(result.IsSuccess);
            Assert.Equal("Api", result.Value.Scope);
            Assert.Equal(1, quotas.Adds);
            Assert.Equal(1, unitOfWork.Saves);
            Assert.NotNull(await quotas.GetAsync(QuotaScope.Api, Ct));
        }

        [Fact]
        public async Task UpdateRejectsAnUnknownScope()
        {
            var handler = new UpdateUsageQuotaHandler(new FakeQuotas(), new FakeUnitOfWork());

            var result = await handler.HandleAsync("Nope", 7, TimeSpan.FromMinutes(3), Ct);

            Assert.True(result.IsFailure);
            Assert.Equal("UNKNOWN_QUOTA_SCOPE", result.Error.Code);
            Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public async Task UpdateRejectsANonPositiveLimit(int limit)
        {
            var handler = new UpdateUsageQuotaHandler(new FakeQuotas(), new FakeUnitOfWork());

            var result = await handler.HandleAsync("Api", limit, TimeSpan.FromMinutes(3), Ct);

            Assert.True(result.IsFailure);
            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
        }

        [Fact]
        public async Task UpdateRejectsAnOutOfRangeWindow()
        {
            var quotas = new FakeQuotas();
            quotas.Seed(UsageQuota.Create(QuotaScope.Api, 100, TimeSpan.FromHours(1)));
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateUsageQuotaHandler(quotas, unitOfWork);

            var result = await handler.HandleAsync("Api", 100, TimeSpan.FromHours(25), Ct);

            Assert.True(result.IsFailure);
            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
            Assert.Equal(0, unitOfWork.Saves);
            Assert.Equal(
                TimeSpan.FromHours(1),
                (await quotas.GetAsync(QuotaScope.Api, Ct))!.Window
            );
        }
    }
}
