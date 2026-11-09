using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobScrape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlyNewToScrapeRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "only_new",
                table: "scrape_runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "only_new", table: "scrape_runs");
        }
    }
}
