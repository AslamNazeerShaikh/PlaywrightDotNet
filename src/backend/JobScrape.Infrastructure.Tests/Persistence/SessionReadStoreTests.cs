using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Sessions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Infrastructure.Persistence;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Persistence
{
    [Trait("Category", "Integration")]
    public sealed class SessionReadStoreTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ListsByNameAndEvaluatesLaunchabilityOnTheClock()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var cooling = BrowserProfile.Create(
                "zeta",
                SourceKey.Naukri,
                BrowserEngine.Chromium,
                BrowserChannel.Bundled
            );
            cooling.MarkVerified(Now);
            cooling.MarkChallenged(ChallengeKind.Captcha, Now, TimeSpan.FromHours(1));
            await using (var scope = database.Scope())
            {
                var profiles = scope.Get<IBrowserProfileRepository>();
                await profiles.AddAsync(cooling, Ct);
                await profiles.AddAsync(
                    BrowserProfile.Create(
                        "alpha",
                        SourceKey.Indeed,
                        BrowserEngine.Firefox,
                        BrowserChannel.Bundled
                    ),
                    Ct
                );
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var during = new SessionReadStore(reload.Db, new FakeTimeProvider(Now.AddMinutes(10)));
            var after = new SessionReadStore(reload.Db, new FakeTimeProvider(Now.AddHours(2)));

            var list = await during.ListAsync(Ct);
            Assert.Equal(["alpha", "zeta"], list.Select(s => s.Name));
            Assert.Equal("Firefox", list[0].Engine);
            Assert.True(list[0].CanLaunch);
            Assert.Equal("CoolingDown", list[1].State);
            Assert.False(list[1].CanLaunch);
            Assert.True((await after.GetAsync(cooling.Id.Value, Ct))!.CanLaunch);
            Assert.Null(await during.GetAsync(Guid.NewGuid(), Ct));
            Assert.Null(await during.GetAsync(Guid.Empty, Ct));
            Assert.Empty(reload.Db.ChangeTracker.Entries());
        }

        [Fact]
        public async Task RegisteredStoreAndNameCheckWork()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            var profiles = scope.Get<IBrowserProfileRepository>();
            await profiles.AddAsync(
                BrowserProfile.Create(
                    "main",
                    SourceKey.Naukri,
                    BrowserEngine.Chromium,
                    BrowserChannel.Bundled
                ),
                Ct
            );

            Assert.True(await profiles.NameExistsAsync("main", Ct));
            await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            await using var reload = database.Scope();
            Assert.True(await reload.Get<IBrowserProfileRepository>().NameExistsAsync("main", Ct));
            Assert.False(
                await reload.Get<IBrowserProfileRepository>().NameExistsAsync("other", Ct)
            );
            Assert.Single(await reload.Get<ISessionStore>().ListAsync(Ct));
        }

        [Fact]
        public void ConstructorRejectsNulls()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SessionReadStore(null!, TimeProvider.System)
            );
        }
    }
}
