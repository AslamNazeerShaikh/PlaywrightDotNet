using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobScrape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FoldProgressIntoRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IRREVERSIBLE: the DELETE below permanently removes orphan
            // progress rows whose run no longer exists. The Down() method
            // only drops the foreign key and cannot restore those rows, so
            // rolling this migration back loses that data forever.
            migrationBuilder.Sql(
                "DELETE FROM scrape_run_progress WHERE id NOT IN (SELECT id FROM scrape_runs)"
            );
            migrationBuilder.AddForeignKey(
                name: "FK_scrape_run_progress_scrape_runs_id",
                table: "scrape_run_progress",
                column: "id",
                principalTable: "scrape_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_scrape_run_progress_scrape_runs_id",
                table: "scrape_run_progress"
            );
        }
    }
}
