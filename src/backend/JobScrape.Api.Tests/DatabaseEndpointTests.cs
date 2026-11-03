using System.Net;
using System.Net.Http.Json;
using JobScrape.Api;
using JobScrape.Api.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class DatabaseEndpointTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public DatabaseEndpointTests(ApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public void RejectsNullExplorer()
        {
            Assert.Throws<ArgumentNullException>(() => new DatabaseController(null!));
        }

        [Fact]
        public async Task ListsTables()
        {
            using var client = _factory.CreateClient();
            using var response = await client.GetAsync(
                "api/v1/db/tables",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<TablesResponse>(
                TestContext.Current.CancellationToken
            );
            Assert.NotNull(body);
            Assert.Contains(body!.Items, t => t.Name == "search_profiles");
        }

        [Fact]
        public async Task GetsSchema()
        {
            using var client = _factory.CreateClient();
            using var response = await client.GetAsync(
                "api/v1/db/tables/search_profiles/schema",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task RowsAndCrudRoundTrip()
        {
            using var client = _factory.CreateClient();
            // insert
            var insert = await client.PostAsJsonAsync(
                "api/v1/db/tables/source_circuit_breakers/rows",
                new { source_key = "testsrc", state = "Closed", consecutive_failures = 0, recent_failures = "[]" },
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.NoContent, insert.StatusCode);

            // list
            var rows = await client.GetAsync(
                "api/v1/db/tables/source_circuit_breakers/rows?offset=0&limit=10",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.OK, rows.StatusCode);

            // delete
            var del = await client.DeleteAsync(
                "api/v1/db/tables/source_circuit_breakers/rows/testsrc",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        }

        [Fact]
        public async Task UnknownTableReturnsNotFound()
        {
            using var client = _factory.CreateClient();
            using var response = await client.GetAsync(
                "api/v1/db/tables/does_not_exist/schema",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        private sealed record TablesResponse(List<TableInfo> Items);
        private sealed record TableInfo(string Name, long RowCount);
    }
}