using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobScrape.Api.Controllers;
using JobScrape.Application.Features.Sessions;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class SessionsEndpointTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public void RejectsNullHandlers()
        {
            Assert.Throws<ArgumentNullException>(() => new SessionsController(null!, null!, null!));
        }

        [Fact]
        public async Task CreateThenGetAndListRoundTripThroughSqlite()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();

            using var created = await client.PostAsJsonAsync(
                "api/v1/sessions",
                new { name = "naukri-main", sourceKey = "naukri" },
                Ct
            );
            var session = await created.Content.ReadFromJsonAsync<SessionResponse>(Ct);

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal(
                $"/api/v1/sessions/{session!.Id}",
                created.Headers.Location!.OriginalString
            );
            Assert.Equal("New", session.State);
            Assert.Equal("Chromium", session.Engine);
            Assert.Equal("naukri/naukri-main", session.UserDataDirectory);
            Assert.True(session.CanLaunch);

            var fetched = await client.GetFromJsonAsync<SessionResponse>(
                $"api/v1/sessions/{session.Id}",
                Ct
            );
            Assert.Equal("naukri-main", fetched!.Name);

            var list = await client.GetFromJsonAsync<SessionListResponse>("api/v1/sessions", Ct);
            Assert.Equal(session.Id, Assert.Single(list!.Items).Id);
        }

        [Fact]
        public async Task InvalidFieldsAreKeyedByJsonName()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                "api/v1/sessions",
                new
                {
                    name = "../escape",
                    sourceKey = "naukri",
                    engine = "Firefox",
                    channel = "Chrome",
                },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            var errors = body.RootElement.GetProperty("errors");
            Assert.True(errors.TryGetProperty("name", out _));
            Assert.Contains(
                "Chromium",
                errors.GetProperty("channel")[0].GetString(),
                StringComparison.Ordinal
            );
        }

        [Fact]
        public async Task DuplicateNameConflictsAndUnknownIdIsNotFound()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();
            using var first = await client.PostAsJsonAsync(
                "api/v1/sessions",
                new { name = "dup", sourceKey = "naukri" },
                Ct
            );

            using var second = await client.PostAsJsonAsync(
                "api/v1/sessions",
                new { name = "dup", sourceKey = "indeed" },
                Ct
            );
            using var missing = await client.GetAsync($"api/v1/sessions/{Guid.NewGuid()}", Ct);
            using var empty = await client.GetAsync($"api/v1/sessions/{Guid.Empty}", Ct);

            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            Assert.Contains(
                "SESSION_NAME_TAKEN",
                await second.Content.ReadAsStringAsync(Ct),
                StringComparison.Ordinal
            );
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Contains(
                "SESSION_NOT_FOUND",
                await missing.Content.ReadAsStringAsync(Ct),
                StringComparison.Ordinal
            );
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        }
    }
}
