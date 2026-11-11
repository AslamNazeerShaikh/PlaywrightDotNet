using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobScrape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigrateChannelsToBundled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE browser_profiles SET channel = 'Bundled' WHERE channel IN ('Chrome','Edge');"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The original Chrome/Edge values cannot be reconstructed.
        }
    }
}
