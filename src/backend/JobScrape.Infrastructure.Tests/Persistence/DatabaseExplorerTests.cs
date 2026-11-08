using JobScrape.Infrastructure.Persistence;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Persistence
{
    [Trait("Category", "Integration")]
    public sealed class DatabaseExplorerTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

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
