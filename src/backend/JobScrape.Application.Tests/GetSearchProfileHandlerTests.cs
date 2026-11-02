using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Application.Results;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class GetSearchProfileHandlerTests
    {
        [Fact]
        public async Task ReturnsProfileOnHit()
        {
            var id = Guid.NewGuid();
            var expected = new SearchProfileResponse(
                id,
                "Backend",
                "naukri",
                "c#",
                ["Pune"],
                null,
                null,
                false,
                [],
                3,
                60,
                60,
                2500,
                6000,
                true,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch
            );
            var handler = new GetSearchProfileHandler(new StubStore(expected));

            var result = await handler.HandleAsync(id, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(expected, result.Value);
        }

        [Fact]
        public async Task EmptyIdFailsValidationWithoutTouchingStore()
        {
            var store = new StubStore(null);
            var handler = new GetSearchProfileHandler(store);

            var result = await handler.HandleAsync(
                Guid.Empty,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.Validation, result.Error.Kind);
            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
            Assert.False(store.WasCalled);
        }

        [Fact]
        public async Task MissingProfileFailsNotFound()
        {
            var id = Guid.NewGuid();
            var handler = new GetSearchProfileHandler(new StubStore(null));

            var result = await handler.HandleAsync(id, TestContext.Current.CancellationToken);

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
            Assert.Equal("PROFILE_NOT_FOUND", result.Error.Code);
        }

        [Fact]
        public void RejectsNullStore()
        {
            Assert.Throws<ArgumentNullException>(() => new GetSearchProfileHandler(null!));
        }

        private sealed class StubStore : ISearchProfileStore
        {
            private readonly SearchProfileResponse? _profile;

            public StubStore(SearchProfileResponse? profile)
            {
                _profile = profile;
            }

            public bool WasCalled { get; private set; }

            public Task<SearchProfileResponse?> GetAsync(
                Guid id,
                CancellationToken cancellationToken
            )
            {
                WasCalled = true;
                return Task.FromResult(_profile);
            }

            public Task<IReadOnlyList<SearchProfileResponse>> ListAsync(
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult<IReadOnlyList<SearchProfileResponse>>([]);
            }
        }
    }
}
