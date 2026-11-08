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
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
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
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            using var response = await client.GetAsync(
                "api/v1/db/tables/search_profiles/schema",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ScrapeRunsSchemaExposesOnDeleteOnTheForeignKey()
        {
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            using var response = await client.GetAsync(
                "api/v1/db/tables/scrape_runs/schema",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
                TestContext.Current.CancellationToken
            );
            var foreignKeys = body.GetProperty("foreignKeys");
            var foreignKey = Assert.Single(foreignKeys.EnumerateArray());
            Assert.Equal("profile_id", foreignKey.GetProperty("column").GetString());
            Assert.True(
                foreignKey.TryGetProperty("onDelete", out var onDelete),
                "FK payload must carry the camelCase onDelete property."
            );
            Assert.False(foreignKey.TryGetProperty("OnDelete", out _));
            Assert.Equal(
                "RESTRICT",
                onDelete.GetString()?.Trim().ToUpperInvariant()
            );
        }

        [Fact]
        public async Task RowsAndCrudRoundTrip()
        {
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            // insert
            var insert = await client.PostAsJsonAsync(
                "api/v1/db/tables/source_circuit_breakers/rows",
                new
                {
                    source_key = "testsrc",
                    state = "Closed",
                    consecutive_failures = 0,
                    recent_failures = "[]",
                },
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.NoContent, insert.StatusCode);

            // list
            var rows = await client.GetAsync(
                "api/v1/db/tables/source_circuit_breakers/rows?offset=0&limit=10",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.OK, rows.StatusCode);

            // update
            var update = await client.PutAsJsonAsync(
                "api/v1/db/tables/source_circuit_breakers/rows",
                new { primaryKeyValue = "testsrc", values = new { consecutive_failures = 3 } },
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

            // unknown key reports not found
            var missing = await client.PutAsJsonAsync(
                "api/v1/db/tables/source_circuit_breakers/rows",
                new { primaryKeyValue = "no-such-row", values = new { consecutive_failures = 3 } },
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

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
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            using var response = await client.GetAsync(
                "api/v1/db/tables/does_not_exist/schema",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UnknownTableSchemaReturnsTableNotFoundCode()
        {
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            using var response = await client.GetAsync(
                "api/v1/db/tables/does_not_exist/schema",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
                TestContext.Current.CancellationToken
            );
            Assert.Equal("TABLE_NOT_FOUND", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task UnknownTableRowsReturnsTableNotFoundCode()
        {
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            using var response = await client.GetAsync(
                "api/v1/db/tables/does_not_exist/rows?offset=0&limit=10",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
                TestContext.Current.CancellationToken
            );
            Assert.Equal("TABLE_NOT_FOUND", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task UpdateUnknownRowReturnsRowNotFoundCode()
        {
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            var missing = await client.PutAsJsonAsync(
                "api/v1/db/tables/source_circuit_breakers/rows",
                new
                {
                    primaryKeyValue = "no-such-row-xyz",
                    values = new { consecutive_failures = 3 },
                },
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            var body = await missing.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
                TestContext.Current.CancellationToken
            );
            Assert.Equal("ROW_NOT_FOUND", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task DeleteUnknownRowReturnsRowNotFoundCode()
        {
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            using var response = await client.DeleteAsync(
                "api/v1/db/tables/source_circuit_breakers/rows/no-such-row-xyz",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
                TestContext.Current.CancellationToken
            );
            Assert.Equal("ROW_NOT_FOUND", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task InvalidTableNameReturnsInvalidTableCode()
        {
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            using var response = await client.GetAsync(
                "api/v1/db/tables/bad-table!/schema",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
                TestContext.Current.CancellationToken
            );
            Assert.Equal("INVALID_TABLE", body.GetProperty("code").GetString());
            Assert.Equal("Invalid table.", body.GetProperty("title").GetString());
            Assert.Equal(
                "Invalid table name. (Parameter 'tableName')",
                body.GetProperty("detail").GetString()
            );
        }

        [Fact]
        public async Task InvalidTableNameOnRowsReturnsInvalidTableCode()
        {
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            using var response = await client.GetAsync(
                "api/v1/db/tables/bad-table!/rows?offset=0&limit=10",
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
                TestContext.Current.CancellationToken
            );
            Assert.Equal("INVALID_TABLE", body.GetProperty("code").GetString());
            Assert.Equal("Invalid table.", body.GetProperty("title").GetString());
        }

        [Fact]
        public async Task InsertWithoutValidColumnsReturnsInvalidInputCode()
        {
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            using var response = await client.PostAsJsonAsync(
                "api/v1/db/tables/source_circuit_breakers/rows",
                new { },
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
                TestContext.Current.CancellationToken
            );
            Assert.Equal("INVALID_INPUT", body.GetProperty("code").GetString());
            Assert.Equal("Invalid input.", body.GetProperty("title").GetString());
            Assert.Equal(
                "No valid columns supplied. (Parameter 'values')",
                body.GetProperty("detail").GetString()
            );
        }

        [Fact]
        public async Task UpdateWithoutPrimaryKeyReturnsInvalidInputCode()
        {
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            using var response = await client.PutAsJsonAsync(
                "api/v1/db/tables/source_circuit_breakers/rows",
                new { primaryKeyValue = "", values = new { consecutive_failures = 3 } },
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
                TestContext.Current.CancellationToken
            );
            Assert.Equal("INVALID_INPUT", body.GetProperty("code").GetString());
            Assert.Equal("Invalid input.", body.GetProperty("title").GetString());
            Assert.Equal("PrimaryKeyValue is required.", body.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task DuplicateInsertReturnsDbErrorCode()
        {
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            var ct = TestContext.Current.CancellationToken;
            var row = new
            {
                source_key = "dberr-src",
                state = "Closed",
                consecutive_failures = 0,
                recent_failures = "[]",
            };
            using (
                var first = await client.PostAsJsonAsync(
                    "api/v1/db/tables/source_circuit_breakers/rows",
                    row,
                    ct
                )
            )
            {
                Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
            }

            try
            {
                using var second = await client.PostAsJsonAsync(
                    "api/v1/db/tables/source_circuit_breakers/rows",
                    row,
                    ct
                );
                Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
                var body = await second.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(ct);
                Assert.Equal("DB_ERROR", body.GetProperty("code").GetString());
                Assert.Equal("Database operation failed.", body.GetProperty("title").GetString());
                Assert.Equal(
                    "The database operation failed.",
                    body.GetProperty("detail").GetString()
                );
            }
            finally
            {
                using var deleted = await client.DeleteAsync(
                    "api/v1/db/tables/source_circuit_breakers/rows/dberr-src",
                    ct
                );
                Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            }
        }

        [Fact]
        public async Task IdentityTablesAreHiddenFromTheExplorer()
        {
            using var client = await _factory.CreateAdminClientAsync(
                TestContext.Current.CancellationToken
            );
            var ct = TestContext.Current.CancellationToken;

            using var tables = await client.GetAsync("api/v1/db/tables", ct);
            var body = await tables.Content.ReadFromJsonAsync<TablesResponse>(ct);
            Assert.DoesNotContain(body!.Items, t => t.Name == "users");
            Assert.DoesNotContain(body.Items, t => t.Name == "roles");

            foreach (var path in new[] { "users", "roles" })
            {
                using var schema = await client.GetAsync($"api/v1/db/tables/{path}/schema", ct);
                Assert.Equal(HttpStatusCode.NotFound, schema.StatusCode);
                var problem = await schema.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
                    ct
                );
                Assert.Equal("TABLE_NOT_FOUND", problem.GetProperty("code").GetString());

                using var rows = await client.GetAsync($"api/v1/db/tables/{path}/rows", ct);
                Assert.Equal(HttpStatusCode.NotFound, rows.StatusCode);
            }
        }

        private sealed record TablesResponse(List<TableInfo> Items);

        private sealed record TableInfo(string Name, long RowCount);
    }
}
