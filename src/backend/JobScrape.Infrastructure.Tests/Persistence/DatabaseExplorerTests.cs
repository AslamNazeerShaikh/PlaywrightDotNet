using JobScrape.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Persistence
{
    [Trait("Category", "Integration")]
    public sealed class DatabaseExplorerTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ListRowsPagesInPrimaryKeyOrder()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using (var connection = new SqliteConnection(database.ConnectionString))
            {
                await connection.OpenAsync(Ct);
                await using var create = connection.CreateCommand();
                // A text primary key is a separate index: an unordered
                // SELECT * scans the table in rowid (insertion) order, so the
                // pages would not ascend and a boundary could repeat or skip.
                create.CommandText = "CREATE TABLE paged (code TEXT PRIMARY KEY, name TEXT);";
                await create.ExecuteNonQueryAsync(Ct);
                foreach (
                    var (code, name) in new[]
                    {
                        ("c", "3"),
                        ("a", "1"),
                        ("e", "5"),
                        ("b", "2"),
                        ("d", "4"),
                    }
                )
                {
                    await using var insert = connection.CreateCommand();
                    insert.CommandText = "INSERT INTO paged (code, name) VALUES (@code, @name);";
                    insert.Parameters.AddWithValue("@code", code);
                    insert.Parameters.AddWithValue("@name", name);
                    await insert.ExecuteNonQueryAsync(Ct);
                }
            }

            var explorer = new SqliteDatabaseExplorer(database.ConnectionString);
            var codes = new List<string>();
            for (var offset = 0; offset < 5; offset += 2)
            {
                var page = await explorer.ListRowsAsync("paged", offset, 2, Ct);
                codes.AddRange(page.Rows.Select(row => row["code"]!.ToString()!));
            }

            Assert.Equal(["a", "b", "c", "d", "e"], codes);
        }

        [Fact]
        public async Task GetSchemaReadsTheForeignKeyOnDeleteActions()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var explorer = new SqliteDatabaseExplorer(database.ConnectionString);

            var runs = await explorer.GetSchemaAsync("scrape_runs", Ct);
            var progress = await explorer.GetSchemaAsync("scrape_run_progress", Ct);

            Assert.NotNull(runs);
            Assert.NotNull(progress);
            var runsFk = Assert.Single(runs!.ForeignKeys);
            Assert.Equal("profile_id", runsFk.Column);
            Assert.Equal("search_profiles", runsFk.ReferencedTable);
            Assert.Equal("id", runsFk.ReferencedColumn);
            Assert.Equal("RESTRICT", runsFk.OnDelete?.ToUpperInvariant());

            var progressFk = Assert.Single(progress!.ForeignKeys);
            Assert.Equal("id", progressFk.Column);
            Assert.Equal("scrape_runs", progressFk.ReferencedTable);
            Assert.Equal("id", progressFk.ReferencedColumn);
            Assert.Equal("CASCADE", progressFk.OnDelete?.ToUpperInvariant());
        }
    }
}
