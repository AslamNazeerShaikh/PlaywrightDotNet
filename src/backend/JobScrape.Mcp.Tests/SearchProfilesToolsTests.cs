using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Mcp.Tools;
using ModelContextProtocol;
using Xunit;

namespace JobScrape.Mcp.Tests
{
    public sealed class SearchProfilesToolsTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static SearchProfileResponse Profile(Guid? id = null)
        {
            return new SearchProfileResponse(
                id ?? Guid.NewGuid(),
                "Backend",
                "naukri",
                "c#",
                ["Pune"],
                null,
                null,
                false,
                [],
                [],
                3,
                1,
                60,
                60,
                2500,
                6000,
                true,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch
            );
        }

        private static SearchProfilesTools Tools(StubStore store)
        {
            return new SearchProfilesTools(
                new ListSearchProfilesHandler(store),
                new GetSearchProfileHandler(store),
                new FakeCaller()
            );
        }

        [Fact]
        public async Task ListProfilesReturnsEveryProfile()
        {
            var profiles = new[] { Profile(), Profile() };

            var response = await Tools(new StubStore(profiles)).ListProfilesAsync(Ct);

            Assert.Equal(2, response.Total);
            Assert.Equal(profiles, response.Items);
        }

        [Fact]
        public async Task GetProfileReturnsProfile()
        {
            var profile = Profile();

            var response = await Tools(new StubStore([profile]))
                .GetProfileAsync(profile.Id.ToString(), Ct);

            Assert.Equal(profile, response);
        }

        [Fact]
        public async Task GetProfileBadFormatFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new StubStore([])).GetProfileAsync("not-a-guid", Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task GetProfileEmptyIdFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new StubStore([])).GetProfileAsync(Guid.Empty.ToString(), Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task GetProfileMissingFailsNotFound()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new StubStore([])).GetProfileAsync(Guid.NewGuid().ToString(), Ct)
            );

            Assert.StartsWith("PROFILE_NOT_FOUND:", error.Message);
        }

        [Fact]
        public void RejectsNulls()
        {
            var store = new StubStore([]);
            var caller = new FakeCaller();
            var list = new ListSearchProfilesHandler(store);
            var get = new GetSearchProfileHandler(store);
            Assert.Throws<ArgumentNullException>(() => new SearchProfilesTools(null!, get, caller));
            Assert.Throws<ArgumentNullException>(() =>
                new SearchProfilesTools(list, null!, caller)
            );
            Assert.Throws<ArgumentNullException>(() => new SearchProfilesTools(list, get, null!));
        }

        private sealed class FakeCaller : ICallerContext
        {
            public string ActorId => "tests";

            public IReadOnlyCollection<string> Roles => [];
        }

        private sealed class StubStore(IReadOnlyList<SearchProfileResponse> profiles)
            : ISearchProfileStore
        {
            public Task<SearchProfileResponse?> GetAsync(
                Guid id,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(profiles.FirstOrDefault(p => p.Id == id));
            }

            public Task<IReadOnlyList<SearchProfileResponse>> ListAsync(
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(profiles);
            }
        }
    }
}
