using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Infrastructure.SearchProfiles;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class InMemorySearchProfileStoreTests
    {
        [Fact]
        public async Task AddThenGetReturnsProfile()
        {
            var store = new InMemorySearchProfileStore();
            var profile = new SearchProfileResponse(Guid.NewGuid(), "Backend", "naukri");
            store.Add(profile);

            var found = await store.GetAsync(profile.Id, TestContext.Current.CancellationToken);

            Assert.Equal(profile, found);
        }

        [Fact]
        public async Task GetMissingReturnsNull()
        {
            var store = new InMemorySearchProfileStore();

            var found = await store.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

            Assert.Null(found);
        }

        [Fact]
        public void AddRejectsNull()
        {
            var store = new InMemorySearchProfileStore();

            Assert.Throws<ArgumentNullException>(() => store.Add(null!));
        }
    }
}
