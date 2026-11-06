using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobScrape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyUniquenessGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_idempotency_keys_owner_key_endpoint_key",
                table: "idempotency_keys",
                columns: new[] { "owner_key", "endpoint", "key" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_idempotency_keys_owner_key_endpoint_key",
                table: "idempotency_keys"
            );
        }
    }
}
