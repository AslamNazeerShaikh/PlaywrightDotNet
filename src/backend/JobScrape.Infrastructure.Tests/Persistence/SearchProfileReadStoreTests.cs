using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Persistence;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Persistence
{
    [Trait("Category", "Integration")]
    public sealed class SearchProfileReadStoreTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static SearchProfile Profile(string name)
        {
            return SearchProfile.Create(
                name,
                SourceKey.Naukri,
                new SearchCriteria("c#", ["Pune"], 6, null, remoteOnly: false),
                new RunLimits(2, 40, 0, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(7)),
                Now
            );
        }

        [Fact]
        public async Task ListsByNameAndGetsByIdWithoutTracking()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var beta = Profile("beta");
            await using (var scope = database.Scope())
            {
                var profiles = scope.Get<ISearchProfileRepository>();
                await profiles.AddAsync(beta, Ct);
                await profiles.AddAsync(Profile("alpha"), Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var store = reload.Get<ISearchProfileStore>();
            var list = await store.ListAsync(Ct);
            var one = await store.GetAsync(beta.Id.Value, Ct);

            Assert.Equal(["alpha", "beta"], list.Select(p => p.Name));
            Assert.NotNull(one);
            Assert.Equal("beta", one.Name);
            Assert.Equal(2, one.MaxPages);
            Assert.Equal(3000, one.MinDelayMs);
            Assert.Equal(6, one.ExperienceYears);
            Assert.Equal(Now, one.CreatedAt);
            Assert.Empty(reload.Db.ChangeTracker.Entries());
            Assert.Null(await store.GetAsync(Guid.NewGuid(), Ct));
            Assert.Null(await store.GetAsync(Guid.Empty, Ct));
            Assert.Throws<ArgumentNullException>(() => new SearchProfileReadStore(null!));
        }

        [Fact]
        public async Task NameExistsSeesSavedAndStagedProfiles()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using (var scope = database.Scope())
            {
                await scope.Get<ISearchProfileRepository>().AddAsync(Profile("saved"), Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var profiles = reload.Get<ISearchProfileRepository>();
            await profiles.AddAsync(Profile("staged"), Ct);

            Assert.True(await profiles.NameExistsAsync("saved", Ct));
            Assert.True(await profiles.NameExistsAsync("staged", Ct));
            Assert.False(await profiles.NameExistsAsync("other", Ct));
        }
    }
}
